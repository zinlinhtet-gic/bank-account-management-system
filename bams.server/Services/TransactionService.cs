using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;

namespace bams.server.Services;

/// <summary>
/// Cash deposits, cash withdrawals and transfers between accounts in this bank. All complete immediately.
/// </summary>
public sealed class TransactionService : ITransactionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly LedgerPostingService _ledger;

    public TransactionService(ApplicationDbContext dbContext, LedgerPostingService ledger)
    {
        _dbContext = dbContext;
        _ledger = ledger;
    }

    /// <summary>
    /// Credits cash into an account. Ledger: debit Cash on Hand, credit Customer Deposits.
    /// </summary>
    public Task<TransactionResponse> DepositAsync(
        DepositRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.ValidateAmount(request.Amount);
        TransactionRequestValidator.ValidateCommonFields(request.Description, request.ReferenceNo);

        return _ledger.RunIdempotentPostingAsync(
            idempotencyKey,
            TransactionType.CashDeposit,
            request.Amount,
            actor.UserId,
            async key =>
            {
                var accounts = await _ledger.LockAccountsAsync([request.AccountId], isRefund: false, cancellationToken);
                var account = accounts[request.AccountId];

                var now = DateTime.UtcNow;
                var entity = LedgerPostingService.CreateTransaction(
                    TransactionType.CashDeposit,
                    TransactionStatus.Completed,
                    request.Amount,
                    request.Description,
                    request.ReferenceNo,
                    key,
                    actor.UserId,
                    now);
                await _ledger.PostGlEntryAsync(
                    entity,
                    AccountingConstants.CashOnHandGlCode,
                    EntryType.Debit,
                    null,
                    now,
                    cancellationToken);
                await _ledger.PostCustomerEntryAsync(entity, account, EntryType.Credit, now, cancellationToken);
                _ledger.AddAuditLog(
                    AuditConstants.DepositAction,
                    entity,
                    actor,
                    new { entity.Amount, AccountId = account.Id },
                    now);

                await _dbContext.Transactions.AddAsync(entity, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return LedgerPostingService.ToResponse(entity, null, account.Id);
            },
            cancellationToken);
    }

    /// <summary>
    /// Debits cash from an account after the account-type debit rules pass.
    /// Ledger: debit Customer Deposits, credit Cash on Hand.
    /// </summary>
    public Task<TransactionResponse> WithdrawAsync(
        WithdrawalRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.ValidateAmount(request.Amount);
        TransactionRequestValidator.ValidateCommonFields(request.Description, request.ReferenceNo);

        return _ledger.RunIdempotentPostingAsync(
            idempotencyKey,
            TransactionType.CashWithdrawal,
            request.Amount,
            actor.UserId,
            async key =>
            {
                var accounts = await _ledger.LockAccountsAsync([request.AccountId], isRefund: false, cancellationToken);
                var account = accounts[request.AccountId];

                var now = DateTime.UtcNow;
                await _ledger.EnsureCanDebitAsync(
                    account,
                    request.Amount,
                    DebitPurpose.Withdrawal,
                    now,
                    cancellationToken);

                var entity = LedgerPostingService.CreateTransaction(
                    TransactionType.CashWithdrawal,
                    TransactionStatus.Completed,
                    request.Amount,
                    request.Description,
                    request.ReferenceNo,
                    key,
                    actor.UserId,
                    now);
                await _ledger.PostCustomerEntryAsync(entity, account, EntryType.Debit, now, cancellationToken);
                await _ledger.PostGlEntryAsync(
                    entity,
                    AccountingConstants.CashOnHandGlCode,
                    EntryType.Credit,
                    null,
                    now,
                    cancellationToken);
                _ledger.AddAuditLog(
                    AuditConstants.WithdrawalAction,
                    entity,
                    actor,
                    new { entity.Amount, AccountId = account.Id },
                    now);

                await _dbContext.Transactions.AddAsync(entity, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return LedgerPostingService.ToResponse(entity, account.Id, null);
            },
            cancellationToken);
    }

    /// <summary>
    /// Moves funds between two accounts in this bank as one atomic, completed transaction.
    /// Ledger: debit and credit Customer Deposits for the two accounts.
    /// </summary>
    public Task<TransactionResponse> TransferInternallyAsync(
        InternalTransferRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.ValidateAmount(request.Amount);
        TransactionRequestValidator.EnsureDifferentAccounts(request.SourceAccountId, request.DestinationAccountId);
        TransactionRequestValidator.ValidateCommonFields(request.Description, request.ReferenceNo);

        return _ledger.RunIdempotentPostingAsync(
            idempotencyKey,
            TransactionType.InternalTransfer,
            request.Amount,
            actor.UserId,
            async key =>
            {
                var accounts = await _ledger.LockAccountsAsync(
                    [request.SourceAccountId, request.DestinationAccountId],
                    isRefund: false,
                    cancellationToken);
                var source = accounts[request.SourceAccountId];
                var destination = accounts[request.DestinationAccountId];

                var now = DateTime.UtcNow;
                await _ledger.EnsureCanDebitAsync(
                    source,
                    request.Amount,
                    DebitPurpose.Transfer,
                    now,
                    cancellationToken);

                var entity = LedgerPostingService.CreateTransaction(
                    TransactionType.InternalTransfer,
                    TransactionStatus.Completed,
                    request.Amount,
                    request.Description,
                    request.ReferenceNo,
                    key,
                    actor.UserId,
                    now);
                await _ledger.PostCustomerEntryAsync(entity, source, EntryType.Debit, now, cancellationToken);
                await _ledger.PostCustomerEntryAsync(entity, destination, EntryType.Credit, now, cancellationToken);
                _ledger.AddAuditLog(
                    AuditConstants.InternalTransferAction,
                    entity,
                    actor,
                    new { entity.Amount, SourceAccountId = source.Id, DestinationAccountId = destination.Id },
                    now);

                await _dbContext.Transactions.AddAsync(entity, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return LedgerPostingService.ToResponse(entity, source.Id, destination.Id);
            },
            cancellationToken);
    }
}
