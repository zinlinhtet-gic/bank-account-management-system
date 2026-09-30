using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AccountTransactionService : IAccountTransactionService
{
    private const string AccountOpeningDescription = "Account opening deposit";

    private readonly ApplicationDbContext _dbContext;
    private readonly LedgerPostingService _ledger;

    public AccountTransactionService(
        ApplicationDbContext dbContext,
        LedgerPostingService ledger)
    {
        _dbContext = dbContext;
        _ledger = ledger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountTransactionDetailResponse>> GetAccountTransactionsAsync(
        long accountId,
        CancellationToken cancellationToken)
    {
        if (!await _dbContext.Accounts.AsNoTracking().AnyAsync(account => account.Id == accountId, cancellationToken))
        {
            throw new NotFoundException(MessageCode.AccountNotFound);
        }

        return await _dbContext.AccountTransactions.AsNoTracking()
            .Where(entry => entry.AccountId == accountId)
            .OrderByDescending(entry => entry.CreatedAt)
            // Entries of one posting share a timestamp; the id keeps their order stable.
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new AccountTransactionDetailResponse(
                entry.Id,
                entry.Transaction!.TransactionNo,
                entry.Transaction.TransactionType,
                entry.Transaction.TransactionStatus,
                entry.EntryType,
                entry.Amount,
                entry.LedgerBalanceAfter,
                entry.AvailableBalanceAfter,
                entry.ValueDate,
                entry.PostingDate,
                entry.Description,
                entry.ReferenceNo,
                entry.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RecordAccountOpeningTransactionAsync(
        Account account,
        decimal openingBalance,
        long initiatedByUserId,
        DateTime currentDateTime,
        CancellationToken cancellationToken)
    {
        if (openingBalance <= 0m)
        {
            return;
        }

        // The opening deposit is booked exactly like a teller cash deposit so reports and statements need no
        // special case.
        var entity = LedgerPostingService.CreateTransaction(
            TransactionType.CashDeposit,
            TransactionStatus.Completed,
            openingBalance,
            AccountOpeningDescription,
            referenceNo: account.AccountNo,
            idempotencyKey: null,
            initiatedByUserId,
            currentDateTime);
        await _ledger.PostGlEntryAsync(
            entity,
            AccountingConstants.CashOnHandGlCode,
            EntryType.Debit,
            null,
            currentDateTime,
            cancellationToken);
        await _ledger.PostCustomerEntryAsync(entity, account, EntryType.Credit, currentDateTime, cancellationToken);
        await _dbContext.Transactions.AddAsync(entity, cancellationToken);
    }

    public async Task RecordScheduledTransactionAsync(Transaction transaction, Account account, decimal beforeLedger,
        decimal beforeAvailable, decimal afterLedger, decimal afterAvailable, decimal amount,
        EntryType entryType, DateOnly effectiveDate, string description, string status, DateTime createdAt,
        CancellationToken cancellationToken)
    {
        await _dbContext.AccountTransactions.AddAsync(new AccountTransaction
        {
            TransactionId = transaction.Id, AccountId = account.Id, EntryType = entryType, Amount = amount,
            LedgerBalanceBefore = beforeLedger, LedgerBalanceAfter = afterLedger,
            AvailableBalanceBefore = beforeAvailable, AvailableBalanceAfter = afterAvailable,
            ValueDate = effectiveDate, PostingDate = effectiveDate, Description = description,
            ReferenceNo = transaction.ReferenceNo, Status = status, CreatedAt = createdAt
        }, cancellationToken);
    }
}
