using bams.server.Constants;
using bams.server.Data;
using bams.server.Models.Accounts;
using bams.server.Models.InterestFees;
using bams.server.Models.Security;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Orchestrates atomic scheduled financial postings across the customer and bank ledgers.</summary>
public sealed class ScheduledFinancialPostingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IScheduledTransactionService _transactions;
    private readonly IGeneralLedgerPostingService _generalLedger;
    private readonly IAccountTransactionService _accountTransactions;
    private readonly IAuditLogService _auditLogs;

    public ScheduledFinancialPostingService(
        ApplicationDbContext dbContext,
        IScheduledTransactionService transactions,
        IGeneralLedgerPostingService generalLedger,
        IAccountTransactionService accountTransactions,
        IAuditLogService auditLogs)
    {
        _dbContext = dbContext;
        _transactions = transactions;
        _generalLedger = generalLedger;
        _accountTransactions = accountTransactions;
        _auditLogs = auditLogs;
    }

    public async Task<long> RecordAccrualAsync(
        long accountId, TransactionType transactionType, decimal amount, string description,
        DateOnly effectiveDate, InterestAccrual? interestAccrual, FeeAccrual? feeAccrual,
        CancellationToken cancellationToken)
    {
        ValidateAmount(amount, "Scheduled accrual transactions");
        ValidateAccrualType(transactionType);

        await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var actorId = await GetSystemActorIdAsync(cancellationToken);
        var account = await GetAccountAsync(accountId, cancellationToken);
        var beforeLedger = account.LedgerBalance;
        var beforeAvailable = account.AvailableBalance;
        var now = DateTime.UtcNow;
        var transaction = await _transactions.CreateAsync(actorId, transactionType, amount, description,
            now, effectiveDate, accountId, accrued: true, cancellationToken: cancellationToken);

        await _generalLedger.AddAccrualEntriesAsync(transaction, accountId, transactionType, amount,
            description, effectiveDate, now, cancellationToken);
        await _accountTransactions.RecordScheduledTransactionAsync(transaction, account, beforeLedger,
            beforeAvailable, beforeLedger, beforeAvailable, amount, GetAccrualEntryType(transactionType),
            effectiveDate, description, "Accrued", now, cancellationToken);
        LinkAccrual(transaction.Id, interestAccrual, feeAccrual);
        await _auditLogs.RecordScheduledFinancialLogAsync(actorId, transaction, accountId,
            beforeLedger, beforeAvailable, beforeLedger, beforeAvailable, description, now,
            isAccrual: true, cancellationToken: cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);
        return transaction.Id;
    }

    public async Task<long> PostAsync(
        long accountId, TransactionType transactionType, decimal amount, string description,
        DateOnly postingDate, IReadOnlyCollection<InterestAccrual>? interestAccruals,
        IReadOnlyCollection<FeeAccrual>? feeAccruals, long? fixedDepositId,
        CancellationToken cancellationToken)
    {
        ValidateAmount(amount, "Scheduled financial postings");
        ValidatePostingType(transactionType);

        await using var databaseTransaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var actorId = await GetSystemActorIdAsync(cancellationToken);
        var account = await GetAccountAsync(accountId, cancellationToken);
        var isCredit = transactionType == TransactionType.InterestCredit;
        var beforeLedger = account.LedgerBalance;
        var beforeAvailable = account.AvailableBalance;
        if (!isCredit && amount > beforeAvailable)
        {
            await databaseTransaction.RollbackAsync(cancellationToken);
            return 0;
        }

        var now = DateTime.UtcNow;
        var transaction = await _transactions.CreateAsync(actorId, transactionType, amount, description,
            now, postingDate, accountId, accrued: false, cancellationToken: cancellationToken);
        ApplyAccountBalance(account, amount, isCredit, now);

        await _generalLedger.AddPostingEntriesAsync(transaction, accountId, transactionType, amount,
            description, postingDate, now, cancellationToken);
        await _accountTransactions.RecordScheduledTransactionAsync(transaction, account, beforeLedger,
            beforeAvailable, account.LedgerBalance, account.AvailableBalance, amount,
            isCredit ? EntryType.Credit : EntryType.Debit, postingDate, description, "Posted", now,
            cancellationToken);
        await UpdateFixedDepositPrincipalAsync(account, fixedDepositId, amount, isCredit, now, cancellationToken);
        await MarkAccrualsPostedAsync(transaction.Id, interestAccruals, feeAccruals, now, cancellationToken);
        await _auditLogs.RecordScheduledFinancialLogAsync(actorId, transaction, accountId,
            beforeLedger, beforeAvailable, account.LedgerBalance, account.AvailableBalance,
            description, now, isAccrual: false, cancellationToken: cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);
        return transaction.Id;
    }

    private async Task<Account> GetAccountAsync(long accountId, CancellationToken cancellationToken) =>
        await _dbContext.Accounts.SingleOrDefaultAsync(a => a.Id == accountId, cancellationToken)
        ?? throw new InvalidOperationException($"Account {accountId} no longer exists.");

    private async Task<long> GetSystemActorIdAsync(CancellationToken cancellationToken) =>
        await _dbContext.Users.AsNoTracking()
            .Where(u => u.Username == ScheduledJobConstants.SystemActorUsername && u.Status == UserStatus.Disabled)
            .Select(u => (long?)u.Id).SingleOrDefaultAsync(cancellationToken)
        ?? throw new InvalidOperationException("The scheduled-jobs system actor has not been seeded.");

    private static void ValidateAmount(decimal amount, string operation)
    {
        if (amount <= 0m || decimal.Round(amount, 2, MidpointRounding.AwayFromZero) != amount)
            throw new InvalidOperationException($"{operation} must be positive and rounded to two decimals.");
    }

    private static void ValidateAccrualType(TransactionType type)
    {
        if (type is not (TransactionType.InterestAccrual or TransactionType.MaintenanceAccrual or TransactionType.DormantPenaltyAccrual))
            throw new InvalidOperationException($"Transaction type '{type}' is not an accrual type.");
    }

    private static void ValidatePostingType(TransactionType type)
    {
        if (type is not (TransactionType.InterestCredit or TransactionType.MaintenanceFee or TransactionType.Penalty))
            throw new InvalidOperationException($"Transaction type '{type}' is not supported for scheduled posting.");
    }

    private static EntryType GetAccrualEntryType(TransactionType type) => type switch
    {
        TransactionType.InterestAccrual => EntryType.Credit,
        TransactionType.MaintenanceAccrual or TransactionType.DormantPenaltyAccrual => EntryType.Debit,
        _ => throw new InvalidOperationException($"Transaction type '{type}' is not an accrual type.")
    };

    private static void ApplyAccountBalance(Account account, decimal amount, bool credit, DateTime now)
    {
        account.LedgerBalance = credit ? account.LedgerBalance + amount : account.LedgerBalance - amount;
        account.AvailableBalance = credit ? account.AvailableBalance + amount : account.AvailableBalance - amount;
        account.UpdatedAt = now;
    }

    private async Task UpdateFixedDepositPrincipalAsync(Account account, long? fixedDepositId,
        decimal amount, bool isCredit, DateTime now, CancellationToken cancellationToken)
    {
        if (!isCredit || !fixedDepositId.HasValue) return;
        var deposit = await _dbContext.FixedDeposits.SingleOrDefaultAsync(
            item => item.Id == fixedDepositId.Value && item.AccountId == account.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Fixed deposit {fixedDepositId.Value} was not found for account {account.Id}.");
        deposit.CurrentPrincipal += amount;
        deposit.UpdatedAt = now;
    }

    private async Task MarkAccrualsPostedAsync(long transactionId,
        IReadOnlyCollection<InterestAccrual>? interestAccruals,
        IReadOnlyCollection<FeeAccrual>? feeAccruals, DateTime now,
        CancellationToken cancellationToken)
    {
        if (interestAccruals is not null)
            foreach (var accrual in interestAccruals)
            {
                accrual.Status = "Posted";
                accrual.PostedTransactionId = transactionId;
                accrual.PostedAt = now;
            }

        if (feeAccruals is not null)
            foreach (var accrual in feeAccruals)
            {
                if (_dbContext.Entry(accrual).State == EntityState.Detached)
                    _dbContext.FeeAccruals.Add(accrual);
                accrual.Status = FeeAccrualStatus.Posted;
                accrual.PostedTransactionId = transactionId;
                accrual.PostedAt = now;
            }
        await Task.CompletedTask;
    }

    private static void LinkAccrual(long transactionId, InterestAccrual? interestAccrual, FeeAccrual? feeAccrual)
    {
        if (interestAccrual is not null) interestAccrual.AccruedTransactionId = transactionId;
        if (feeAccrual is not null) feeAccrual.AccruedTransactionId = transactionId;
    }
}
