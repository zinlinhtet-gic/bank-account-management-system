using System.Security.Cryptography;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using bams.server.Utils.Security;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// NRC transfers: a sender pays at the counter (in cash or from their account) for a named receiver, identified by
/// NRC, to collect at one of our branches or at another bank. Neither needs an account. The money waits in the NRC
/// Transfers Payable ledger account until it is paid out, or the transfer is cancelled and the sender refunded.
/// </summary>
public sealed class NrcTransferService : INrcTransferService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly LedgerPostingService _ledger;

    public NrcTransferService(ApplicationDbContext dbContext, LedgerPostingService ledger)
    {
        _dbContext = dbContext;
        _ledger = ledger;
    }

    /// <summary>
    /// Takes the sender's money and creates a pending NRC transfer. The plain pickup code is returned only in this
    /// response; the database keeps only its hash.
    /// Ledger: debit Cash on Hand (paid in cash) or Customer Deposits (paid from an account), credit NRC Transfers Payable.
    /// </summary>
    public async Task<TransactionResponse> CreateTransferAsync(
        NrcTransferRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.ValidateAmount(request.Amount);
        TransactionRequestValidator.EnsureRequired(
            request.SenderName,
            request.SenderNrc,
            request.ReceiverName,
            request.ReceiverNrc);
        ValidateTransferFieldLengths(request);
        TransactionRequestValidator.ValidateCommonFields(request.Description, request.ReferenceNo);
        await ValidatePickupLocationAsync(request, cancellationToken);

        var isAtBranch = request.DeliveryType == TransactionConstants.NrcDeliveryAtBranch;

        return await _ledger.RunIdempotentPostingAsync(
            idempotencyKey,
            TransactionType.NrcTransfer,
            request.Amount,
            actor.UserId,
            // A cash-funded NRC transfer debits no customer account.
            request.SourceAccountId is { } sourceAccountId ? [sourceAccountId] : [],
            async key =>
            {
                var now = DateTime.UtcNow;
                var entity = LedgerPostingService.CreateTransaction(
                    TransactionType.NrcTransfer,
                    TransactionStatus.Pending,
                    request.Amount,
                    request.Description,
                    request.ReferenceNo,
                    key,
                    actor.UserId,
                    now);

                // Money in: from the sender's account after the debit rules, or cash handed over at the counter.
                long? sourceAccountId = null;
                if (request.SourceAccountId is { } accountId)
                {
                    var accounts = await _ledger.LockAccountsAsync([accountId], isRefund: false, cancellationToken);
                    var source = accounts[accountId];
                    await _ledger.EnsureCanDebitAsync(source, request.Amount, DebitPurpose.Transfer, now, cancellationToken);
                    await _ledger.PostCustomerEntryAsync(entity, source, EntryType.Debit, now, cancellationToken);
                    sourceAccountId = source.Id;
                }
                else
                {
                    await _ledger.PostGlEntryAsync(
                        entity,
                        AccountingConstants.CashOnHandGlCode,
                        EntryType.Debit,
                        null,
                        now,
                        cancellationToken);
                }

                await _ledger.PostGlEntryAsync(
                    entity,
                    AccountingConstants.NrcTransfersPayableGlCode,
                    EntryType.Credit,
                    null,
                    now,
                    cancellationToken);

                var pickupCode = GeneratePickupCode();
                await _dbContext.NrcCashTransferDetails.AddAsync(new NrcCashTransferDetail
                {
                    Transaction = entity,
                    SenderName = request.SenderName.Trim(),
                    SenderNrc = request.SenderNrc.Trim(),
                    SenderPhone = TransactionRequestValidator.TrimToNull(request.SenderPhone),
                    ReceiverName = request.ReceiverName.Trim(),
                    ReceiverNrc = request.ReceiverNrc.Trim(),
                    ReceiverPhone = TransactionRequestValidator.TrimToNull(request.ReceiverPhone),
                    DeliveryType = request.DeliveryType,
                    PickupBranchId = isAtBranch ? request.PickupBranchId : null,
                    PickupOtherBankId = isAtBranch ? null : request.PickupOtherBankId,
                    PickupCodeHash = PasswordHasher.HashPassword(pickupCode),
                    PickupExpiresAt = now.Add(TransactionConstants.NrcPickupCodeValidity),
                    Status = TransactionConstants.PickupPendingStatus
                }, cancellationToken);

                // The pickup code is a secret and is deliberately left out of the audit details.
                _ledger.AddAuditLog(
                    AuditConstants.NrcTransferAction,
                    entity,
                    actor,
                    new
                    {
                        entity.Amount,
                        SourceAccountId = sourceAccountId,
                        request.DeliveryType,
                        request.PickupBranchId,
                        request.PickupOtherBankId
                    },
                    now);

                await _dbContext.Transactions.AddAsync(entity, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return LedgerPostingService.ToResponse(entity, sourceAccountId, null, pickupCode);
            },
            cancellationToken);
    }

    /// <summary>
    /// Pays out an NRC transfer in cash at one of our branches, only to the person the sender designated: the name
    /// and NRC read from the collector's card must match the receiver, and the pickup code must be correct. Each
    /// mismatch or wrong code is counted; after <c>TransactionConstants.MaximumFailedPickupAttempts</c> the transfer
    /// is blocked and can only be cancelled.
    /// Ledger: debit NRC Transfers Payable, credit Cash on Hand.
    /// </summary>
    public async Task<TransactionResponse> CompletePickupAsync(
        NrcPickupRequest request,
        RequestActor actor,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.EnsureRequired(request.PickupCode, request.ReceiverName, request.ReceiverNrc);
        TransactionRequestValidator.EnsureMaximumLength(request.ReceiverName, TransactionConstants.PersonNameMaximumLength);
        TransactionRequestValidator.EnsureMaximumLength(request.ReceiverNrc, TransactionConstants.NrcMaximumLength);

        await using (var dbTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            var (entity, detail) = await LockPendingTransferAsync(request.TransactionId, cancellationToken);
            var now = DateTime.UtcNow;

            // Another bank pays out its own transfers; only our branches verify the code here.
            if (detail.DeliveryType != TransactionConstants.NrcDeliveryAtBranch)
            {
                throw new BusinessRuleException(MessageCode.NrcPickupLocationMismatch);
            }

            if (detail.FailedPickupAttempts >= TransactionConstants.MaximumFailedPickupAttempts)
            {
                throw new BusinessRuleException(MessageCode.PickupAttemptsExceeded);
            }

            if (detail.PickupExpiresAt < now)
            {
                throw new BusinessRuleException(MessageCode.PickupCodeExpired);
            }

            // Only the receiver the sender designated may collect. A mismatch is counted like a wrong code, so
            // trying different identities against one transfer is also blocked.
            if (!IsDesignatedReceiver(detail, request.ReceiverName, request.ReceiverNrc))
            {
                await RecordFailedPickupAttemptAsync(entity, detail, actor, MessageCode.NrcPickupReceiverMismatch, now, cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);

                throw new BusinessRuleException(MessageCode.NrcPickupReceiverMismatch);
            }

            if (!PasswordHasher.VerifyPassword(request.PickupCode.Trim(), detail.PickupCodeHash!))
            {
                // Commit the failed attempt before rejecting, so repeated guessing is counted and eventually blocked.
                await RecordFailedPickupAttemptAsync(entity, detail, actor, MessageCode.InvalidPickupCode, now, cancellationToken);
                await dbTransaction.CommitAsync(cancellationToken);

                throw new ValidationException(MessageCode.InvalidPickupCode);
            }

            await _ledger.PostGlEntryAsync(
                entity,
                AccountingConstants.NrcTransfersPayableGlCode,
                EntryType.Debit,
                null,
                now,
                cancellationToken);

            // Money out: cash over the counter, since the receiver need not have an account with us.
            await _ledger.PostGlEntryAsync(
                entity,
                AccountingConstants.CashOnHandGlCode,
                EntryType.Credit,
                null,
                now,
                cancellationToken);

            CompleteTransfer(entity, detail, actor, now);
            _ledger.AddAuditLog(
                AuditConstants.NrcPickupAction,
                entity,
                actor,
                new { entity.Amount, detail.ReceiverName, detail.ReceiverNrc, PaidInCash = true },
                now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }

        return await _ledger.BuildTransactionResponseAsync(request.TransactionId, cancellationToken);
    }

    /// <summary>
    /// Records that the other bank paid out an NRC transfer collected there, and completes it.
    /// Ledger: debit NRC Transfers Payable, credit Due from Other Banks.
    /// </summary>
    public async Task<TransactionResponse> RecordOtherBankPayoutAsync(
        long transactionId,
        NrcPayoutRequest request,
        RequestActor actor,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.EnsureMaximumLength(
            request.PayoutReference,
            TransactionConstants.GatewayReferenceMaximumLength);

        await _ledger.RunInTransactionAsync(async () =>
        {
            var (entity, detail) = await LockPendingTransferAsync(transactionId, cancellationToken);

            // Transfers collected at our branches are paid out with the pickup code instead.
            if (detail.DeliveryType != TransactionConstants.NrcDeliveryAtOtherBank)
            {
                throw new BusinessRuleException(MessageCode.NrcPickupLocationMismatch);
            }

            var now = DateTime.UtcNow;
            await _ledger.PostGlEntryAsync(
                entity,
                AccountingConstants.NrcTransfersPayableGlCode,
                EntryType.Debit,
                null,
                now,
                cancellationToken);
            await _ledger.PostGlEntryAsync(
                entity,
                AccountingConstants.DueFromOtherBanksGlCode,
                EntryType.Credit,
                null,
                now,
                cancellationToken);

            CompleteTransfer(entity, detail, actor, now);
            _ledger.AddAuditLog(
                AuditConstants.NrcPaidOutByOtherBankAction,
                entity,
                actor,
                new
                {
                    entity.Amount,
                    detail.PickupOtherBankId,
                    PayoutReference = TransactionRequestValidator.TrimToNull(request.PayoutReference)
                },
                now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }, cancellationToken);

        return await _ledger.BuildTransactionResponseAsync(transactionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TransactionResponse> ReissuePickupCodeAsync(
        long transactionId,
        RequestActor actor,
        CancellationToken cancellationToken)
    {
        var pickupCode = GeneratePickupCode();

        await _ledger.RunInTransactionAsync(async () =>
        {
            // The same row lock as pickup and cancel, so a reissue cannot race a pickup with the old code.
            var (entity, detail) = await LockPendingTransferAsync(transactionId, cancellationToken);

            var now = DateTime.UtcNow;
            detail.PickupCodeHash = PasswordHasher.HashPassword(pickupCode);
            detail.PickupExpiresAt = now.Add(TransactionConstants.NrcPickupCodeValidity);
            detail.FailedPickupAttempts = 0;
            entity.UpdatedAt = now;

            // The pickup code is a secret and is deliberately left out of the audit details.
            _ledger.AddAuditLog(
                AuditConstants.NrcPickupCodeReissuedAction,
                entity,
                actor,
                new { detail.PickupExpiresAt },
                now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }, cancellationToken);

        var response = await _ledger.BuildTransactionResponseAsync(transactionId, cancellationToken);
        return response with { PickupCode = pickupCode };
    }

    /// <summary>
    /// Cancels a pending NRC transfer (expired, blocked, or at the sender's request) and refunds the sender the way
    /// they paid: into their account, or in cash. Returns the refund (Reversal) transaction.
    /// </summary>
    public async Task<TransactionResponse> CancelTransferAsync(
        long transactionId,
        NrcCancelRequest request,
        RequestActor actor,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.EnsureMaximumLength(request.Reason, TransactionConstants.DescriptionMaximumLength);

        var refundId = await _ledger.RunInTransactionAsync(async () =>
        {
            var (entity, detail) = await LockPendingTransferAsync(transactionId, cancellationToken);

            var now = DateTime.UtcNow;
            var refund = await _ledger.CreateRefundAsync(
                entity,
                AccountingConstants.NrcTransfersPayableGlCode,
                request.Reason,
                actor,
                now,
                cancellationToken);

            entity.TransactionStatus = TransactionStatus.Cancelled;
            entity.UpdatedAt = now;
            detail.Status = TransactionConstants.PickupCancelledStatus;
            detail.PickupCodeHash = null;
            _ledger.AddAuditLog(
                AuditConstants.NrcCancelledAction,
                entity,
                actor,
                new { entity.Amount, RefundTransactionNo = refund.TransactionNo, refund.Description },
                now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return refund.Id;
        }, cancellationToken);

        return await _ledger.BuildTransactionResponseAsync(refundId, cancellationToken);
    }

    // Marks the transfer paid out and deletes the code hash so the code can never be used again.
    private static void CompleteTransfer(Transaction entity, NrcCashTransferDetail detail, RequestActor actor, DateTime now)
    {
        entity.TransactionStatus = TransactionStatus.Completed;
        entity.PostedBy = actor.UserId;
        entity.PostedAt = now;
        entity.UpdatedAt = now;
        detail.PickedUpAt = now;
        detail.PickupVerifiedBy = actor.UserId;
        detail.Status = TransactionConstants.PickupCompletedStatus;
        detail.PickupCodeHash = null;
    }

    // Compares the identity on the collector's NRC card with the receiver the sender designated. Names ignore case
    // and repeated spaces; NRC numbers ignore case and all whitespace, so typing differences are not rejected.
    private static bool IsDesignatedReceiver(NrcCashTransferDetail detail, string receiverName, string receiverNrc)
    {
        return string.Equals(NormalizeName(receiverName), NormalizeName(detail.ReceiverName), StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeNrc(receiverNrc), NormalizeNrc(detail.ReceiverNrc), StringComparison.OrdinalIgnoreCase);
    }

    // Trims a person name and collapses runs of whitespace to one space.
    private static string NormalizeName(string name)
    {
        return string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    // Removes all whitespace from an NRC number.
    private static string NormalizeNrc(string nrc)
    {
        return string.Concat(nrc.Where(character => !char.IsWhiteSpace(character)));
    }

    // Counts and audits a rejected pickup (wrong identity or wrong code) and saves it, so the caller can commit it
    // before rejecting the request.
    private async Task RecordFailedPickupAttemptAsync(
        Transaction entity,
        NrcCashTransferDetail detail,
        RequestActor actor,
        MessageCode reason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        detail.FailedPickupAttempts++;
        _ledger.AddAuditLog(
            AuditConstants.NrcPickupFailedAction,
            entity,
            actor,
            new { detail.FailedPickupAttempts, Reason = reason.ToString() },
            now);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // The receiver collects at an active branch of ours or at a known other bank; the matching id is required.
    private async Task ValidatePickupLocationAsync(NrcTransferRequest request, CancellationToken cancellationToken)
    {
        if (request.DeliveryType == TransactionConstants.NrcDeliveryAtBranch)
        {
            var branchExists = request.PickupBranchId is { } branchId && await _dbContext.Branches
                .AsNoTracking()
                .AnyAsync(branch => branch.Id == branchId && branch.Status == BranchConstants.ActiveStatus, cancellationToken);
            if (!branchExists)
            {
                throw new NotFoundException(MessageCode.BranchNotFound);
            }

            return;
        }

        if (request.DeliveryType == TransactionConstants.NrcDeliveryAtOtherBank)
        {
            var bankExists = request.PickupOtherBankId is { } bankId && await _dbContext.OtherBanks
                .AsNoTracking()
                .AnyAsync(bank => bank.Id == bankId, cancellationToken);
            if (!bankExists)
            {
                throw new NotFoundException(MessageCode.OtherBankNotFound);
            }

            return;
        }

        throw new ValidationException(MessageCode.InvalidRequest);
    }

    // Row-locks the NRC detail so two simultaneous pickups, payouts or cancellations cannot both succeed, then loads
    // the transaction. Anything other than a pending NRC transfer is rejected.
    private async Task<(Transaction Entity, NrcCashTransferDetail Detail)> LockPendingTransferAsync(
        long transactionId,
        CancellationToken cancellationToken)
    {
        var detail = (await _dbContext.NrcCashTransferDetails
                .FromSql($"SELECT * FROM NrcCashTransferDetails WHERE TransactionId = {transactionId} FOR UPDATE")
                .ToListAsync(cancellationToken))
            .SingleOrDefault();
        var entity = await _dbContext.Transactions
            .FirstOrDefaultAsync(item => item.Id == transactionId, cancellationToken);

        if (entity is null
            || detail is null
            || entity.TransactionType != TransactionType.NrcTransfer)
        {
            throw new NotFoundException(MessageCode.TransactionNotFound);
        }

        if (entity.TransactionStatus != TransactionStatus.Pending
            || detail.Status != TransactionConstants.PickupPendingStatus
            || detail.PickupCodeHash is null)
        {
            throw new BusinessRuleException(MessageCode.TransactionNotPendingPickup);
        }

        return (entity, detail);
    }

    // Validates the sender and receiver fields of an NRC transfer against their column sizes.
    private static void ValidateTransferFieldLengths(NrcTransferRequest request)
    {
        TransactionRequestValidator.EnsureMaximumLength(request.SenderName, TransactionConstants.PersonNameMaximumLength);
        TransactionRequestValidator.EnsureMaximumLength(request.SenderNrc, TransactionConstants.NrcMaximumLength);
        TransactionRequestValidator.EnsureMaximumLength(request.SenderPhone, TransactionConstants.PhoneMaximumLength);
        TransactionRequestValidator.EnsureMaximumLength(request.ReceiverName, TransactionConstants.PersonNameMaximumLength);
        TransactionRequestValidator.EnsureMaximumLength(request.ReceiverNrc, TransactionConstants.NrcMaximumLength);
        TransactionRequestValidator.EnsureMaximumLength(request.ReceiverPhone, TransactionConstants.PhoneMaximumLength);
    }

    // Generates a cryptographically random six-digit NRC pickup code.
    private static string GeneratePickupCode()
    {
        return RandomNumberGenerator
            .GetInt32(TransactionConstants.NrcPickupCodeMinimumValue, TransactionConstants.NrcPickupCodeExclusiveMaximum)
            .ToString();
    }
}
