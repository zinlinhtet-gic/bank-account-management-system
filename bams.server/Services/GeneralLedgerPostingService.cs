using bams.server.Constants;
using bams.server.Data;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class GeneralLedgerPostingService(ApplicationDbContext dbContext) : IGeneralLedgerPostingService
{
    public async Task AddAccrualEntriesAsync(Transaction transaction, long accountId, TransactionType type,
        decimal amount, string description, DateOnly postingDate, DateTime createdAt, CancellationToken cancellationToken)
    {
        await EnsurePostingDateIsOpenAsync(postingDate, cancellationToken);
        var (debitCode, debitClass, creditCode, creditClass, creditCustomerId) = type switch
        {
            TransactionType.InterestAccrual => (AccountingConstants.InterestExpenseGlCode, GlAccountClass.Expense,
                AccountingConstants.InterestPayableGlCode, GlAccountClass.Liability, (long?)accountId),
            TransactionType.MaintenanceAccrual => (AccountingConstants.MaintenanceFeeReceivableGlCode, GlAccountClass.Asset,
                AccountingConstants.MaintenanceFeeIncomeGlCode, GlAccountClass.Income, null),
            TransactionType.DormantPenaltyAccrual => (AccountingConstants.DormantPenaltyReceivableGlCode, GlAccountClass.Asset,
                AccountingConstants.DormantPenaltyIncomeGlCode, GlAccountClass.Income, null),
            _ => throw new InvalidOperationException($"Transaction type '{type}' is not an accrual type.")
        };
        var debit = await GetAsync(debitCode, debitClass, cancellationToken);
        var credit = await GetAsync(creditCode, creditClass, cancellationToken);
        dbContext.TransactionEntries.AddRange(
            CreateEntry(transaction.Id, debit.Id, accountId, EntryType.Debit, amount, postingDate, description, createdAt),
            CreateEntry(transaction.Id, credit.Id, creditCustomerId, EntryType.Credit, amount, postingDate, description, createdAt));
    }

    public async Task AddPostingEntriesAsync(Transaction transaction, long accountId, TransactionType type,
        decimal amount, string description, DateOnly postingDate, DateTime createdAt, CancellationToken cancellationToken)
    {
        await EnsurePostingDateIsOpenAsync(postingDate, cancellationToken);
        var isCredit = type == TransactionType.InterestCredit;
        var (offsetCode, offsetClass) = type switch
        {
            TransactionType.InterestCredit => (AccountingConstants.InterestPayableGlCode, GlAccountClass.Liability),
            TransactionType.MaintenanceFee => (AccountingConstants.MaintenanceFeeReceivableGlCode, GlAccountClass.Asset),
            TransactionType.Penalty => (AccountingConstants.DormantPenaltyReceivableGlCode, GlAccountClass.Asset),
            _ => throw new InvalidOperationException($"Transaction type '{type}' is not supported for scheduled posting.")
        };

        // Scheduled postings move customer balances, so they hit the same Customer Deposits account as teller postings.
        var deposit = await GetAsync(AccountingConstants.CustomerDepositsGlCode, GlAccountClass.Liability, cancellationToken);
        var offset = await GetAsync(offsetCode, offsetClass, cancellationToken);
        dbContext.TransactionEntries.AddRange(
            CreateEntry(transaction.Id, deposit.Id, accountId, isCredit ? EntryType.Credit : EntryType.Debit, amount, postingDate, description, createdAt),
            CreateEntry(transaction.Id, offset.Id, null, isCredit ? EntryType.Debit : EntryType.Credit, amount, postingDate, description, createdAt));
    }

    private async Task<GlAccount> GetAsync(string code, GlAccountClass expectedClass, CancellationToken cancellationToken) =>
        await dbContext.GlAccounts.SingleOrDefaultAsync(a => a.Code == code && a.Status == AccountingConstants.ActiveGlAccountStatus && a.AccountClass == expectedClass, cancellationToken)
        ?? throw new InvalidOperationException($"Required active General Ledger account '{code}' is missing.");

    private async Task EnsurePostingDateIsOpenAsync(DateOnly postingDate, CancellationToken cancellationToken)
    {
        var closed = await dbContext.BusinessDates.AsNoTracking()
            .AnyAsync(item => item.Date == postingDate && item.Status == OperationsConstants.BusinessDateClosed, cancellationToken);
        if (closed) throw new BusinessRuleException(MessageCode.BusinessDateClosed);
    }

    private static TransactionEntry CreateEntry(long transactionId, long glAccountId, long? customerAccountId,
        EntryType type, decimal amount, DateOnly date, string description, DateTime createdAt) => new()
    {
        TransactionId = transactionId, GlAccountId = glAccountId, CustomerAccountId = customerAccountId,
        EntryType = type, Amount = amount, PostingDate = date, Description = description, CreatedAt = createdAt
    };
}
