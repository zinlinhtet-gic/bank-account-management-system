using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// Transfers to accounts at other banks. A transfer debits the customer at once and stays pending in the Interbank
/// Clearing ledger account until the payment gateway result is recorded as settled or failed.
/// </summary>
public sealed class InterbankTransferService : IInterbankTransferService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly LedgerPostingService _ledger;

    public InterbankTransferService(ApplicationDbContext dbContext, LedgerPostingService ledger)
    {
        _dbContext = dbContext;
        _ledger = ledger;
    }

    /// <summary>
    /// Debits the source account and records a pending interbank transfer.
    /// Ledger: debit Customer Deposits, credit Interbank Clearing.
    /// </summary>
    public async Task<TransactionResponse> CreateTransferAsync(
        InterbankTransferRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.ValidateAmount(request.Amount);
        TransactionRequestValidator.EnsureRequired(request.DestinationAccountNo, request.BeneficiaryName);
        TransactionRequestValidator.EnsureMaximumLength(
            request.DestinationAccountNo,
            TransactionConstants.DestinationAccountNoMaximumLength);
        TransactionRequestValidator.EnsureMaximumLength(
            request.BeneficiaryName,
            TransactionConstants.PersonNameMaximumLength);
        TransactionRequestValidator.ValidateCommonFields(request.Description, request.ReferenceNo);

        // Check the bank up front; otherwise the foreign key fails on save and the caller gets a 500.
        var otherBankExists = await _dbContext.OtherBanks
            .AsNoTracking()
            .AnyAsync(bank => bank.Id == request.OtherBankId, cancellationToken);
        if (!otherBankExists)
        {
            throw new NotFoundException(MessageCode.OtherBankNotFound);
        }

        return await _ledger.RunIdempotentPostingAsync(
            idempotencyKey,
            TransactionType.InterbankTransfer,
            request.Amount,
            actor.UserId,
            async key =>
            {
                var accounts = await _ledger.LockAccountsAsync([request.SourceAccountId], isRefund: false, cancellationToken);
                var source = accounts[request.SourceAccountId];

                var now = DateTime.UtcNow;
                await _ledger.EnsureCanDebitAsync(
                    source,
                    request.Amount,
                    DebitPurpose.Transfer,
                    now,
                    cancellationToken);

                var entity = LedgerPostingService.CreateTransaction(
                    TransactionType.InterbankTransfer,
                    TransactionStatus.Pending,
                    request.Amount,
                    request.Description,
                    request.ReferenceNo,
                    key,
                    actor.UserId,
                    now);
                await _ledger.PostCustomerEntryAsync(entity, source, EntryType.Debit, now, cancellationToken);
                await _ledger.PostGlEntryAsync(
                    entity,
                    AccountingConstants.InterbankClearingGlCode,
                    EntryType.Credit,
                    null,
                    now,
                    cancellationToken);
                await _dbContext.InterbankTransferDetails.AddAsync(new InterbankTransferDetail
                {
                    Transaction = entity,
                    OtherBankId = request.OtherBankId,
                    DestinationAccountNo = request.DestinationAccountNo.Trim(),
                    BeneficiaryName = request.BeneficiaryName.Trim(),
                    GatewayStatus = GatewayStatus.Pending,
                    RequestAt = now
                }, cancellationToken);
                _ledger.AddAuditLog(
                    AuditConstants.InterbankTransferAction,
                    entity,
                    actor,
                    new
                    {
                        entity.Amount,
                        SourceAccountId = source.Id,
                        request.OtherBankId,
                        DestinationAccountNo = request.DestinationAccountNo.Trim()
                    },
                    now);

                await _dbContext.Transactions.AddAsync(entity, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return LedgerPostingService.ToResponse(entity, source.Id, null);
            },
            cancellationToken);
    }

    /// <summary>
    /// Records that the gateway settled a pending transfer and completes it.
    /// Ledger: debit Interbank Clearing, credit Due from Other Banks.
    /// </summary>
    public async Task<TransactionResponse> CompleteTransferAsync(
        long transactionId,
        InterbankSettlementRequest request,
        RequestActor actor,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.EnsureMaximumLength(
            request.GatewayReference,
            TransactionConstants.GatewayReferenceMaximumLength);
        TransactionRequestValidator.EnsureMaximumLength(
            request.SettlementReference,
            TransactionConstants.GatewayReferenceMaximumLength);

        await _ledger.RunInTransactionAsync(async () =>
        {
            var (entity, detail) = await LockPendingTransferAsync(transactionId, cancellationToken);

            var now = DateTime.UtcNow;
            await _ledger.PostGlEntryAsync(
                entity,
                AccountingConstants.InterbankClearingGlCode,
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

            entity.TransactionStatus = TransactionStatus.Completed;
            entity.PostedBy = actor.UserId;
            entity.PostedAt = now;
            entity.UpdatedAt = now;
            detail.GatewayStatus = GatewayStatus.Success;
            detail.GatewayReference = TransactionRequestValidator.TrimToNull(request.GatewayReference);
            detail.SettlementReference = TransactionRequestValidator.TrimToNull(request.SettlementReference);
            detail.ResponseAt = now;
            _ledger.AddAuditLog(
                AuditConstants.InterbankCompletedAction,
                entity,
                actor,
                new { entity.Amount, detail.GatewayReference, detail.SettlementReference },
                now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return entity.Id;
        }, cancellationToken);

        return await _ledger.BuildTransactionResponseAsync(transactionId, cancellationToken);
    }

    /// <summary>
    /// Records that the gateway rejected a pending transfer, marks it failed and refunds the sender with a Reversal
    /// transaction (returned). Ledger for the refund: debit Interbank Clearing, credit Customer Deposits.
    /// </summary>
    public async Task<TransactionResponse> FailTransferAsync(
        long transactionId,
        InterbankFailureRequest request,
        RequestActor actor,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.EnsureMaximumLength(
            request.GatewayReference,
            TransactionConstants.GatewayReferenceMaximumLength);
        TransactionRequestValidator.EnsureMaximumLength(request.Reason, TransactionConstants.DescriptionMaximumLength);

        var refundId = await _ledger.RunInTransactionAsync(async () =>
        {
            var (entity, detail) = await LockPendingTransferAsync(transactionId, cancellationToken);

            var now = DateTime.UtcNow;
            var refund = await _ledger.CreateRefundAsync(
                entity,
                AccountingConstants.InterbankClearingGlCode,
                request.Reason,
                actor,
                now,
                cancellationToken);

            entity.TransactionStatus = TransactionStatus.Failed;
            entity.UpdatedAt = now;
            detail.GatewayStatus = GatewayStatus.Failed;
            detail.GatewayReference = TransactionRequestValidator.TrimToNull(request.GatewayReference);
            detail.ResponseAt = now;
            _ledger.AddAuditLog(
                AuditConstants.InterbankFailedAction,
                entity,
                actor,
                new { entity.Amount, detail.GatewayReference, RefundTransactionNo = refund.TransactionNo, refund.Description },
                now);

            await _dbContext.SaveChangesAsync(cancellationToken);
            return refund.Id;
        }, cancellationToken);

        return await _ledger.BuildTransactionResponseAsync(refundId, cancellationToken);
    }

    // Row-locks the interbank detail so the gateway result can be recorded only once, then loads the transaction.
    // Anything other than a pending interbank transfer is rejected.
    private async Task<(Transaction Entity, InterbankTransferDetail Detail)> LockPendingTransferAsync(
        long transactionId,
        CancellationToken cancellationToken)
    {
        var detail = (await _dbContext.InterbankTransferDetails
                .FromSql($"SELECT * FROM InterbankTransferDetails WHERE TransactionId = {transactionId} FOR UPDATE")
                .ToListAsync(cancellationToken))
            .SingleOrDefault();
        var entity = await _dbContext.Transactions
            .FirstOrDefaultAsync(item => item.Id == transactionId, cancellationToken);

        if (entity is null
            || detail is null
            || entity.TransactionType != TransactionType.InterbankTransfer)
        {
            throw new NotFoundException(MessageCode.TransactionNotFound);
        }

        if (entity.TransactionStatus != TransactionStatus.Pending || detail.GatewayStatus != GatewayStatus.Pending)
        {
            throw new BusinessRuleException(MessageCode.TransactionNotPending);
        }

        return (entity, detail);
    }
}
