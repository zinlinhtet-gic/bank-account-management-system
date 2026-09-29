using bams.server.Data;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class GeneralLedgerPostingService(ApplicationDbContext dbContext) : IGeneralLedgerPostingService
{
    private const string DepositLiability = "2001", InterestExpense = "6001", InterestPayable = "2101";
    private const string MaintenanceReceivable = "1101", PenaltyReceivable = "1102";
    private const string MaintenanceIncome = "4001", PenaltyIncome = "4002";

    public async Task AddAccrualEntriesAsync(Transaction transaction, long accountId, TransactionType type,
        decimal amount, string description, DateOnly postingDate, DateTime createdAt, CancellationToken cancellationToken)
    {
        var (debitCode, debitClass, creditCode, creditClass, creditCustomerId) = type switch
        {
            TransactionType.InterestAccrual => (InterestExpense, GlAccountClass.Expense, InterestPayable, GlAccountClass.Liability, (long?)accountId),
            TransactionType.MaintenanceAccrual => (MaintenanceReceivable, GlAccountClass.Asset, MaintenanceIncome, GlAccountClass.Income, null),
            TransactionType.DormantPenaltyAccrual => (PenaltyReceivable, GlAccountClass.Asset, PenaltyIncome, GlAccountClass.Income, null),
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
        var isCredit = type == TransactionType.InterestCredit;
        var (offsetCode, offsetClass) = type switch
        {
            TransactionType.InterestCredit => (InterestPayable, GlAccountClass.Liability),
            TransactionType.MaintenanceFee => (MaintenanceReceivable, GlAccountClass.Asset),
            TransactionType.Penalty => (PenaltyReceivable, GlAccountClass.Asset),
            _ => throw new InvalidOperationException($"Transaction type '{type}' is not supported for scheduled posting.")
        };
        var deposit = await GetAsync(DepositLiability, GlAccountClass.Liability, cancellationToken);
        var offset = await GetAsync(offsetCode, offsetClass, cancellationToken);
        dbContext.TransactionEntries.AddRange(
            CreateEntry(transaction.Id, deposit.Id, accountId, isCredit ? EntryType.Credit : EntryType.Debit, amount, postingDate, description, createdAt),
            CreateEntry(transaction.Id, offset.Id, null, isCredit ? EntryType.Debit : EntryType.Credit, amount, postingDate, description, createdAt));
    }

    private async Task<GlAccount> GetAsync(string code, GlAccountClass expectedClass, CancellationToken cancellationToken) =>
        await dbContext.GlAccounts.SingleOrDefaultAsync(a => a.Code == code && a.Status == "Active" && a.AccountClass == expectedClass, cancellationToken)
        ?? throw new InvalidOperationException($"Required active General Ledger account '{code}' is missing.");

    private static TransactionEntry CreateEntry(long transactionId, long glAccountId, long? customerAccountId,
        EntryType type, decimal amount, DateOnly date, string description, DateTime createdAt) => new()
    {
        TransactionId = transactionId, GlAccountId = glAccountId, CustomerAccountId = customerAccountId,
        EntryType = type, Amount = amount, PostingDate = date, Description = description, CreatedAt = createdAt
    };
}
