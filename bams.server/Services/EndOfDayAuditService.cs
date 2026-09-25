using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Audit;
using bams.server.Exceptions;
using bams.server.Models.Accounting;
using bams.server.Models.External;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using bams.server.Messages;

namespace bams.server.Services;

public sealed class EndOfDayAuditService: IEndOfDayAuditService
{
    private readonly ApplicationDbContext _dbContext;

    public EndOfDayAuditService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Runs the complete end-of-day accounting audit,
    /// reconciliation and daily summary generation.
    /// </summary>
    public async Task<EndOfDayAuditResult>
        RunEndOfDayAuditAsync(
            DateOnly auditDate,
            CancellationToken cancellationToken)
    {
        // Prevent accidental duplicate EOD execution.
        await EnsureAuditHasNotAlreadyRunAsync(
            auditDate,
            cancellationToken);

        /*
         * Everything after validation should be atomic.
         *
         * If reconciliation or DailySummary creation fails,
         * nothing should remain partially saved.
         */
        await using var dbTransaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        // 1. Retrieve transactions occurring on the requested date.
        var transactions =
            await GetTransactionsForAuditDateAsync(
                auditDate,
                cancellationToken);

        // 2. Include only valid posted financial transactions.
        var includedTransactions = transactions
            .Where(IsValidPostedTransaction)
            .ToList();

        var excludedTransactionCount =
            transactions.Count
            - includedTransactions.Count;

        // 3. Classify transaction types.
        // Keep this because it corresponds to the flowchart.
        var transactionTypeCounts =
            ClassifyTransactions(
                includedTransactions);

        // 4. Retrieve the corresponding GL entries.
        var accountingEntries =
            await GetAccountingEntriesAsync(
                includedTransactions,
                auditDate,
                cancellationToken);

        // 5. Ensure each included transaction actually has GL entries.
        var transactionsWithoutEntries =
            GetTransactionIdsWithoutAccountingEntries(
                includedTransactions,
                accountingEntries);

        if (transactionsWithoutEntries.Count > 0)
        {
            throw new BusinessRuleException(
                MessageCode.BusinessRuleViolation);
        }

        // 6. Find transaction-level debit/credit mismatches.
        var unbalancedTransactionIds =
            GetUnbalancedTransactionIds(
                accountingEntries);

        if (unbalancedTransactionIds.Count > 0)
        {
            throw new BusinessRuleException(
                MessageCode.BusinessRuleViolation);
        }

        // 7. Group entries by GL account.
        var glAccountTotals =
            CalculateGlAccountTotals(
                accountingEntries);

        // 8. Calculate total daily debit.
        var totalDebit =
            CalculateTotalDebit(
                accountingEntries);

        // 9. Calculate total daily credit.
        var totalCredit =
            CalculateTotalCredit(
                accountingEntries);

        // 10. Final double-entry balance check.
        var isBalanced =
            totalDebit == totalCredit;

        if (!isBalanced)
        {
            throw new BusinessRuleException(
                MessageCode.BusinessRuleViolation);
        }

        // 11. Resolve the user performing the reconciliation.
        //
        // TEMPORARY development behavior:
        // later replace this with the authenticated auditor/current user.
        var performedBy =
            await GetPerformedByUserIdAsync(
                cancellationToken);

        // 12. Create the reconciliation batch.
        var reconciliationBatch =
            await CreateReconciliationBatchAsync(
                auditDate,
                totalDebit,
                totalCredit,
                performedBy,
                cancellationToken);

        // 13. Create reconciliation item records.
        var reconciliationItems =
            CreateReconciliationItems(
                reconciliationBatch.Id,
                accountingEntries);

        _dbContext.ReconciliationItems.AddRange(
            reconciliationItems);

        // 14. Generate GL DailySummary records.
        var dailySummaries =
            await CreateDailySummariesAsync(
                auditDate,
                glAccountTotals,
                cancellationToken);

        _dbContext.DailySummaries.AddRange(
            dailySummaries);

        // 15. Save reconciliation items and summaries together.
        await _dbContext.SaveChangesAsync(
            cancellationToken);

        // 16. Everything succeeded.
        await dbTransaction.CommitAsync(
            cancellationToken);

        return new EndOfDayAuditResult(
            auditDate,
            transactions.Count,
            includedTransactions.Count,
            excludedTransactionCount,
            totalDebit,
            totalCredit,
            true,
            true);
    }

    // Retrieves all transactions initiated on the requested audit date.
    private async Task<IReadOnlyList<Transaction>>
        GetTransactionsForAuditDateAsync(
            DateOnly auditDate,
            CancellationToken cancellationToken)
    {
        var startUtc =
            auditDate.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Utc);

        var endUtc =
            auditDate
                .AddDays(1)
                .ToDateTime(
                    TimeOnly.MinValue,
                    DateTimeKind.Utc);

        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.TransactionAt >= startUtc &&
                transaction.TransactionAt < endUtc)
            .ToListAsync(
                cancellationToken);
    }

    // Determines whether the transaction should participate
    // in end-of-day accounting.
    private static bool IsValidPostedTransaction(
        Transaction transaction)
    {
        return transaction.PostedAt.HasValue &&
            transaction.TransactionStatus is
                TransactionStatus.Posted or
                TransactionStatus.Completed;
    }

    // Groups included transactions by their business type.
    private static IReadOnlyDictionary<TransactionType, int>
        ClassifyTransactions(
            IReadOnlyCollection<Transaction> transactions)
    {
        return transactions
            .GroupBy(transaction =>
                transaction.TransactionType)
            .ToDictionary(
                group => group.Key,
                group => group.Count());
    }

    // Retrieves accounting entries belonging to included transactions.
    private async Task<IReadOnlyList<TransactionEntry>>
        GetAccountingEntriesAsync(
            IReadOnlyCollection<Transaction> transactions,
            DateOnly auditDate,
            CancellationToken cancellationToken)
    {
        if (transactions.Count == 0)
        {
            return Array.Empty<TransactionEntry>();
        }

        var transactionIds = transactions
            .Select(transaction => transaction.Id)
            .ToList();

        return await _dbContext.TransactionEntries
            .AsNoTracking()
            .Where(entry =>
                transactionIds.Contains(
                    entry.TransactionId) &&
                entry.PostingDate == auditDate)
            .ToListAsync(
                cancellationToken);
    }

    // Finds transactions that have no accounting entries.
    private static IReadOnlyList<long>
        GetTransactionIdsWithoutAccountingEntries(
            IReadOnlyCollection<Transaction> transactions,
            IReadOnlyCollection<TransactionEntry> entries)
    {
        var transactionIdsWithEntries =
            entries
                .Select(entry =>
                    entry.TransactionId)
                .ToHashSet();

        return transactions
            .Where(transaction =>
                !transactionIdsWithEntries.Contains(
                    transaction.Id))
            .Select(transaction =>
                transaction.Id)
            .ToList();
    }

    // Finds transactions whose debit total does not equal
    // their credit total.
    private static IReadOnlyList<long>
        GetUnbalancedTransactionIds(
            IReadOnlyCollection<TransactionEntry> entries)
    {
        return entries
            .GroupBy(entry =>
                entry.TransactionId)
            .Where(group =>
            {
                var debit = group
                    .Where(entry =>
                        entry.EntryType ==
                        EntryType.Debit)
                    .Sum(entry =>
                        entry.Amount);

                var credit = group
                    .Where(entry =>
                        entry.EntryType ==
                        EntryType.Credit)
                    .Sum(entry =>
                        entry.Amount);

                return debit != credit;
            })
            .Select(group =>
                group.Key)
            .ToList();
    }

    // Groups accounting activity by GL account.
    private static IReadOnlyList<GlAuditTotal>
        CalculateGlAccountTotals(
            IReadOnlyCollection<TransactionEntry> entries)
    {
        return entries
            .GroupBy(entry =>
                entry.GlAccountId)
            .Select(group =>
                new GlAuditTotal(
                    group.Key,

                    group
                        .Where(entry =>
                            entry.EntryType ==
                            EntryType.Debit)
                        .Sum(entry =>
                            entry.Amount),

                    group
                        .Where(entry =>
                            entry.EntryType ==
                            EntryType.Credit)
                        .Sum(entry =>
                            entry.Amount)))
            .ToList();
    }

    // Calculates the total debit for the audit date.
    private static decimal CalculateTotalDebit(
        IReadOnlyCollection<TransactionEntry> entries)
    {
        return entries
            .Where(entry =>
                entry.EntryType ==
                EntryType.Debit)
            .Sum(entry =>
                entry.Amount);
    }

    // Calculates the total credit for the audit date.
    private static decimal CalculateTotalCredit(
        IReadOnlyCollection<TransactionEntry> entries)
    {
        return entries
            .Where(entry =>
                entry.EntryType ==
                EntryType.Credit)
            .Sum(entry =>
                entry.Amount);
    }

    // Prevents duplicate EOD processing.
    private async Task EnsureAuditHasNotAlreadyRunAsync(
        DateOnly auditDate,
        CancellationToken cancellationToken)
    {
        var reconciliationExists =
            await _dbContext.ReconciliationBatches
                .AsNoTracking()
                .AnyAsync(
                    batch =>
                        batch.ReconciliationDate ==
                            auditDate &&
                        batch.ReconciliationType ==
                            AuditConstants
                                .EndOfDayReconciliationType,
                    cancellationToken);

        if (reconciliationExists)
        {
            throw new ConflictException(
                MessageCode.Conflict);
        }

        var summaryExists =
            await _dbContext.DailySummaries
                .AsNoTracking()
                .AnyAsync(
                    summary =>
                        summary.SummaryDate ==
                            auditDate,
                    cancellationToken);

        if (summaryExists)
        {
            throw new ConflictException(
                MessageCode.Conflict);
        }
    }

    // Temporary development implementation.
    // Replace with the authenticated auditor/current-user service later.
    private async Task<long> GetPerformedByUserIdAsync(
        CancellationToken cancellationToken)
    {
        var userId =
            await _dbContext.Users
                .AsNoTracking()
                .OrderBy(user =>
                    user.Id)
                .Select(user =>
                    (long?)user.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (!userId.HasValue)
        {
            throw new BusinessRuleException(
                MessageCode.BusinessRuleViolation);
        }

        return userId.Value;
    }

    // Creates the overall reconciliation batch.
    private async Task<ReconciliationBatch>
        CreateReconciliationBatchAsync(
            DateOnly auditDate,
            decimal totalDebit,
            decimal totalCredit,
            long performedBy,
            CancellationToken cancellationToken)
    {
        var batch =
            new ReconciliationBatch
            {
                ReconciliationDate =
                    auditDate,

                ReconciliationType =
                    AuditConstants
                        .EndOfDayReconciliationType,

                Status =
                    AuditConstants
                        .ReconciliationCompletedStatus,

                TotalDebit =
                    totalDebit,

                TotalCredit =
                    totalCredit,

                Difference =
                    totalDebit - totalCredit,

                PerformedBy =
                    performedBy,

                PerformedAt =
                    DateTime.UtcNow
            };

        _dbContext.ReconciliationBatches.Add(
            batch);

        await _dbContext.SaveChangesAsync(
            cancellationToken);

        return batch;
    }

    // Creates item-level reconciliation trace records.
    private static IReadOnlyList<ReconciliationItem>
        CreateReconciliationItems(
            long reconciliationBatchId,
            IReadOnlyCollection<TransactionEntry> entries)
    {
        return entries
            .Select(entry =>
            {
                var expectedAmount =
                    entry.Amount;

                var actualAmount =
                    entry.Amount;

                return new ReconciliationItem
                {
                    ReconciliationBatchId =
                        reconciliationBatchId,

                    TransactionId =
                        entry.TransactionId,

                    TransactionEntryId =
                        entry.Id,

                    ExpectedAmount =
                        expectedAmount,

                    ActualAmount =
                        actualAmount,

                    Difference =
                        actualAmount -
                        expectedAmount,

                    Status =
                        AuditConstants
                            .ReconciliationMatchedStatus,

                    Note =
                        "Matched during end-of-day internal reconciliation."
                };
            })
            .ToList();
    }

    // Gets the latest closing balance before the audit date.
    private async Task<decimal> GetOpeningBalanceAsync(
        long glAccountId,
        DateOnly auditDate,
        CancellationToken cancellationToken)
    {
        var previousSummary =
            await _dbContext.DailySummaries
                .AsNoTracking()
                .Where(summary =>
                    summary.GlAccountId ==
                        glAccountId &&
                    summary.SummaryDate <
                        auditDate)
                .OrderByDescending(summary =>
                    summary.SummaryDate)
                .Select(summary => new
                {
                    summary.ClosingBalance
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        return previousSummary?.ClosingBalance
            ?? 0m;
    }

    // Calculates the closing balance using the normal balance
    // of the GL account class.
    private static decimal CalculateClosingBalance(
        GlAccountClass accountClass,
        decimal openingBalance,
        decimal totalDebit,
        decimal totalCredit)
    {
        return accountClass switch
        {
            GlAccountClass.Asset or
            GlAccountClass.Expense
                => openingBalance
                    + totalDebit
                    - totalCredit,

            GlAccountClass.Liability or
            GlAccountClass.Equity or
            GlAccountClass.Income
                => openingBalance
                    + totalCredit
                    - totalDebit,

            _ => throw new BusinessRuleException(
                MessageCode.BusinessRuleViolation)
        };
    }

    // Generates DailySummary records for all affected GL accounts.
    private async Task<IReadOnlyList<DailySummary>>
        CreateDailySummariesAsync(
            DateOnly auditDate,
            IReadOnlyCollection<GlAuditTotal> glAccountTotals,
            CancellationToken cancellationToken)
    {
        if (glAccountTotals.Count == 0)
        {
            return Array.Empty<DailySummary>();
        }

        var glAccountIds =
            glAccountTotals
                .Select(total =>
                    total.GlAccountId)
                .Distinct()
                .ToList();

        var glAccounts =
            await _dbContext.GlAccounts
                .AsNoTracking()
                .Where(account =>
                    glAccountIds.Contains(
                        account.Id))
                .ToDictionaryAsync(
                    account =>
                        account.Id,
                    cancellationToken);

        if (glAccounts.Count !=
            glAccountIds.Count)
        {
            throw new NotFoundException(
                MessageCode.ResourceNotFound);
        }

        var summaries =
            new List<DailySummary>();

        foreach (var total in glAccountTotals)
        {
            if (!glAccounts.TryGetValue(
                    total.GlAccountId,
                    out var glAccount))
            {
                throw new NotFoundException(
                    MessageCode.ResourceNotFound);
            }

            var openingBalance =
                await GetOpeningBalanceAsync(
                    total.GlAccountId,
                    auditDate,
                    cancellationToken);

            var closingBalance =
                CalculateClosingBalance(
                    glAccount.AccountClass,
                    openingBalance,
                    total.TotalDebit,
                    total.TotalCredit);

            summaries.Add(
                new DailySummary
                {
                    SummaryDate =
                        auditDate,

                    GlAccountId =
                        total.GlAccountId,

                    OpeningBalance =
                        openingBalance,

                    TotalDebit =
                        total.TotalDebit,

                    TotalCredit =
                        total.TotalCredit,

                    ClosingBalance =
                        closingBalance,

                    GeneratedAt =
                        DateTime.UtcNow
                });
        }

        return summaries;
    }
}