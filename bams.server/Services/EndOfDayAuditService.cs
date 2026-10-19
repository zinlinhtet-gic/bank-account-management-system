using System.Text.Json;
using bams.server.Utils;
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
using System.Security.Claims;

namespace bams.server.Services;

public sealed class EndOfDayAuditService: IEndOfDayAuditService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ReconciliationExclusionService _exclusions;

    public EndOfDayAuditService(ApplicationDbContext dbContext, IHttpContextAccessor httpContextAccessor,
        ReconciliationExclusionService exclusions)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _exclusions = exclusions;
    }

    /// <summary>
    /// Runs the complete end-of-day accounting audit,
    /// reconciliation and daily summary generation.
    /// </summary>
    public async Task<EndOfDayAuditResult>RunEndOfDayAuditAsync(DateOnly auditDate, CancellationToken cancellationToken,
        bool allowAccountingExceptions = false)
    {
        bool ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? dbTransaction = ownsTransaction
            ? await _dbContext.Database.BeginTransactionAsync(cancellationToken) : null;
        await _dbContext.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(cancellationToken);
        await _dbContext.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Date = {auditDate} FOR UPDATE").ToListAsync(cancellationToken);
        await EnsureAuditHasNotAlreadyRunAsync(auditDate, cancellationToken);
        IReadOnlyList<Transaction> transactions = await GetTransactionsForAuditDateAsync(auditDate, cancellationToken);
        List<Transaction> included = transactions.Where(transaction => IsValidPostedTransaction(transaction) &&
            transaction.TransactionType != TransactionType.AccountPostingRepair && transaction.AccountingRootTransactionId == null).ToList();
        IReadOnlyList<TransactionEntry> journalEntries = await GetAccountingEntriesAsync(included, cancellationToken);
        IReadOnlyList<long> missing = GetTransactionIdsWithoutAccountingEntries(included, journalEntries);
        HashSet<long> waivedTransactions = await _exclusions.ActiveSourceIdsAsync(auditDate, "Transaction", cancellationToken);
        HashSet<long> waivedLines = await _exclusions.ActiveSourceIdsAsync(auditDate, "GL", cancellationToken);
        IReadOnlyList<long> unbalanced = GetUnbalancedTransactionIds(journalEntries);
        List<TransactionEntry> checkedJournal = journalEntries.Where(entry => !waivedLines.Contains(entry.Id) &&
            !waivedTransactions.Contains(entry.Transaction?.AccountingRootTransactionId ?? entry.TransactionId)).ToList();
        IReadOnlyList<long> effectiveUnbalanced = GetUnbalancedTransactionIds(checkedJournal);
        bool hasMissing = missing.Any(id => !waivedTransactions.Contains(id));
        if (!allowAccountingExceptions && hasMissing) throw new BusinessRuleException(MessageCode.TransactionMissingAccountingEntries);
        if (!allowAccountingExceptions && effectiveUnbalanced.Count > 0) throw new BusinessRuleException(MessageCode.TransactionEntriesUnbalanced);

        // Summaries retain every actual posting on this date; effective repairs and waivers only affect validation.
        List<TransactionEntry> dayEntries = await _dbContext.TransactionEntries.AsNoTracking().Include(x => x.Transaction)
            .Where(entry => entry.PostingDate == auditDate).ToListAsync(cancellationToken);
        decimal totalDebit = CalculateTotalDebit(dayEntries);
        decimal totalCredit = CalculateTotalCredit(dayEntries);
        List<ReconciliationLedgerAmount> effective = await ReconciliationLedgerQueries.Amounts(_dbContext)
            .Where(entry => entry.ReconciliationDate == auditDate && !waivedLines.Contains(entry.Id) &&
                !waivedTransactions.Contains(entry.RootTransactionId)).ToListAsync(cancellationToken);
        decimal effectiveDebit = effective.Where(entry => entry.Direction == EntryType.Debit).Sum(entry => entry.Amount);
        decimal effectiveCredit = effective.Where(entry => entry.Direction == EntryType.Credit).Sum(entry => entry.Amount);
        bool isBalanced = effectiveDebit == effectiveCredit;
        bool isReconciled = isBalanced && !hasMissing && effectiveUnbalanced.Count == 0;
        if (!isBalanced && !allowAccountingExceptions) throw new BusinessRuleException(MessageCode.DailyAccountingUnbalanced);
        List<JournalFinding> journalFindings = journalEntries.GroupBy(entry => entry.Transaction?.AccountingRootTransactionId ?? entry.TransactionId)
            .Select(group => new JournalFinding(group.Key,
                group.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount),
                group.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount)))
            .Where(finding => finding.Debit != finding.Credit).OrderBy(finding => finding.TransactionId).ToList();
        IReadOnlyList<long> rawUnbalanced = GetUnbalancedTransactionIds(journalEntries, groupRepairs: false);
        List<JournalFinding> rawJournalFindings = journalEntries.GroupBy(entry => entry.TransactionId)
            .Select(group => new JournalFinding(group.Key,
                group.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount),
                group.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount)))
            .Where(finding => finding.Debit != finding.Credit).OrderBy(finding => finding.TransactionId).ToList();
        AuditFindings findings = new(allowAccountingExceptions, missing, journalFindings, rawJournalFindings, rawUnbalanced,
            totalDebit, totalCredit, effectiveDebit, effectiveCredit,
            waivedTransactions.Order().ToList(), waivedLines.Order().ToList(), isReconciled);
        bool hasFindings = !isReconciled || missing.Count > 0 || unbalanced.Count > 0 || rawUnbalanced.Count > 0 ||
            totalDebit != totalCredit || waivedTransactions.Count > 0 || waivedLines.Count > 0;
        ReconciliationBatch batch = await CreateReconciliationBatchAsync(auditDate, totalDebit, totalCredit,
            GetPerformedByUserId(), cancellationToken);
        batch.FindingsJson = JsonSerializer.Serialize(findings);
        batch.Status = hasFindings ? AuditConstants.ReconciliationCompletedWithExceptionsStatus : AuditConstants.ReconciliationCompletedStatus;
        IReadOnlyList<ReconciliationItem> items = CreateReconciliationItems(batch.Id, dayEntries);
        foreach (ReconciliationItem item in items)
        {
            TransactionEntry entry = dayEntries.Single(line => line.Id == item.TransactionEntryId);
            long rootId = entry.Transaction?.AccountingRootTransactionId ?? entry.TransactionId;
            bool waived = waivedTransactions.Contains(rootId) || waivedLines.Contains(entry.Id);
            bool failed = effectiveUnbalanced.Contains(rootId) || !isBalanced;
            bool repaired = rawUnbalanced.Contains(entry.TransactionId) && !effectiveUnbalanced.Contains(rootId);
            item.Status = waived ? AuditConstants.ReconciliationWaivedStatus : failed ? AuditConstants.ReconciliationExceptionStatus : AuditConstants.ReconciliationMatchedStatus;
            item.Note = waived ? "Excluded by an explicit source waiver; original entry retained." :
                failed ? "Accounting inequality retained during force close; see batch findings." :
                repaired ? "Validated with linked journal repair components; raw posting retained." : "Matched during end-of-day internal reconciliation.";
        }
        _dbContext.ReconciliationItems.AddRange(items);
        _dbContext.DailySummaries.AddRange(await CreateDailySummariesAsync(auditDate, CalculateGlAccountTotals(dayEntries), cancellationToken));
        await _dbContext.SaveChangesAsync(cancellationToken);
        if (dbTransaction is not null) await dbTransaction.CommitAsync(cancellationToken);
        return new EndOfDayAuditResult(auditDate, transactions.Count, included.Count, transactions.Count - included.Count,
            totalDebit, totalCredit, isBalanced, isReconciled);
    }

    private sealed record JournalFinding(long TransactionId, decimal Debit, decimal Credit);
    private sealed record AuditFindings(bool Forced, IReadOnlyList<long> MissingJournalTransactionIds,
        IReadOnlyList<JournalFinding> UnbalancedJournals, IReadOnlyList<JournalFinding> RawUnbalancedJournals, IReadOnlyList<long> RawUnbalancedTransactionIds,
        decimal RawDebit, decimal RawCredit, decimal EffectiveDebit, decimal EffectiveCredit,
        IReadOnlyList<long> WaivedTransactionIds, IReadOnlyList<long> WaivedEntryIds, bool IsReconciled);

    // Retrieves the transactions of one Myanmar business day: those made during the local day, plus those whose
    // ledger lines are posted on that date (scheduled postings carry their own business date).
    private async Task<IReadOnlyList<Transaction>> GetTransactionsForAuditDateAsync(DateOnly auditDate, CancellationToken cancellationToken)
    {
        (DateTime startUtc, DateTime endUtc) = BusinessTime.GetUtcRange(auditDate);

        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BusinessDate == auditDate ||
                (transaction.TransactionAt >= startUtc && transaction.TransactionAt < endUtc) ||
                _dbContext.TransactionEntries.Any(entry =>
                    (entry.TransactionId == transaction.Id || entry.Transaction!.AccountingRootTransactionId == transaction.Id) &&
                    entry.PostingDate == auditDate))
            .ToListAsync(cancellationToken);
    }

    // Determines whether the transaction should participate
    // in end-of-day accounting.
    // Accruals (interest expense / payable, fee receivables) are real ledger lines, so they are included too;
    // leaving them out would make the payable and receivable GL balances drift once the accruals are settled.
    private static bool IsValidPostedTransaction(Transaction transaction)
    {
        return transaction.PostedAt.HasValue &&
            transaction.TransactionStatus is
                TransactionStatus.Posted or
                TransactionStatus.Completed or
                TransactionStatus.Accrued;
    }

    // Retrieves accounting entries belonging to included transactions.
    private async Task<IReadOnlyList<TransactionEntry>> GetAccountingEntriesAsync(
        IReadOnlyCollection<Transaction> transactions, CancellationToken cancellationToken)
    {
        List<long> ids = transactions.Select(x => x.Id).ToList();
        List<TransactionEntry> entries = await _dbContext.TransactionEntries.AsNoTracking().Include(x => x.Transaction)
            .Where(x => ids.Contains(x.TransactionId) || x.Transaction!.AccountingRootTransactionId.HasValue && ids.Contains(x.Transaction.AccountingRootTransactionId.Value))
            .ToListAsync(cancellationToken);
        return entries;
    }

    // Finds transactions that have no accounting entries.
    private static IReadOnlyList<long> GetTransactionIdsWithoutAccountingEntries(
            IReadOnlyCollection<Transaction> transactions,
            IReadOnlyCollection<TransactionEntry> entries)
    {
        HashSet<long> transactionIdsWithEntries =
            entries
                .Select(entry => entry.Transaction?.AccountingRootTransactionId ?? entry.TransactionId)
                .ToHashSet();

        return transactions
            .Where(transaction => !transactionIdsWithEntries.Contains(transaction.Id))
            .Select(transaction => transaction.Id)
            .ToList();
    }

    // Finds transactions whose debit total does not equal
    // their credit total.
    private static IReadOnlyList<long> GetUnbalancedTransactionIds(IReadOnlyCollection<TransactionEntry> entries, bool groupRepairs = true)
    {
        return entries
            .GroupBy(entry => groupRepairs ? entry.Transaction?.AccountingRootTransactionId ?? entry.TransactionId : entry.TransactionId)
            .Where(group =>
            {
                decimal debit = group
                    .Where(entry => entry.EntryType == EntryType.Debit)
                    .Sum(entry => entry.Amount);

                decimal credit = group
                    .Where(entry => entry.EntryType == EntryType.Credit)
                    .Sum(entry => entry.Amount);

                return debit != credit;
            })
            .Select(group => group.Key)
            .ToList();
    }

    // Groups accounting activity by GL account.
    private static IReadOnlyList<GlAuditTotal> CalculateGlAccountTotals(IReadOnlyCollection<TransactionEntry> entries)
    {
        return entries
            .GroupBy(entry =>entry.GlAccountId)
            .Select(group =>
                new GlAuditTotal(
                    group.Key,
                    group
                        .Where(entry => entry.EntryType == EntryType.Debit)
                        .Sum(entry => entry.Amount),

                    group
                        .Where(entry => entry.EntryType == EntryType.Credit)
                        .Sum(entry => entry.Amount)))
            .ToList();
    }

    // Calculates the total debit for the audit date.
    private static decimal CalculateTotalDebit(IReadOnlyCollection<TransactionEntry> entries)
    {
        return entries
            .Where(entry => entry.EntryType == EntryType.Debit)
            .Sum(entry => entry.Amount);
    }

    // Calculates the total credit for the audit date.
    private static decimal CalculateTotalCredit(IReadOnlyCollection<TransactionEntry> entries)
    {
        return entries
            .Where(entry => entry.EntryType == EntryType.Credit)
            .Sum(entry => entry.Amount);
    }

    // Prevents duplicate EOD processing.
    private async Task EnsureAuditHasNotAlreadyRunAsync(DateOnly auditDate,CancellationToken cancellationToken)
    {
        bool reconciliationExists = await _dbContext.ReconciliationBatches
                .AsNoTracking()
                .AnyAsync(batch => batch.ReconciliationDate ==auditDate &&
                        batch.ReconciliationType == AuditConstants.EndOfDayReconciliationType,cancellationToken);

        if (reconciliationExists)
        {
            throw new ConflictException(MessageCode.EndOfDayAuditAlreadyProcessed);
        }

        bool summaryExists =await _dbContext.DailySummaries
                .AsNoTracking()
                .AnyAsync(
                    summary => summary.SummaryDate == auditDate,
                    cancellationToken);

        if (summaryExists)
        {
            throw new ConflictException(MessageCode.EndOfDayAuditAlreadyProcessed);
        }
    }

    // Returns the authenticated user ID that started the end-of-day audit.
    private long GetPerformedByUserId()
    {
        System.Security.Claims.ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
        if(user?.Identity?.IsAuthenticated != true)
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }
        string? userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if(!long.TryParse(userIdValue, out long userId))
        {
            throw new ForbiddenException(MessageCode.Forbidden);
        }
        return userId;
    }
    // Creates the overall reconciliation batch.
    private async Task<ReconciliationBatch>CreateReconciliationBatchAsync(
            DateOnly auditDate,
            decimal totalDebit,
            decimal totalCredit,
            long performedBy,
            CancellationToken cancellationToken)
    {
        ReconciliationBatch batch =new ReconciliationBatch
            {
                ReconciliationDate =auditDate,
                ReconciliationType =AuditConstants.EndOfDayReconciliationType,
                Status = AuditConstants.ReconciliationCompletedStatus,
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                Difference = totalDebit - totalCredit,
                PerformedBy = performedBy,
                PerformedAt = DateTime.UtcNow
            };
        _dbContext.ReconciliationBatches.Add(batch);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return batch;
    }

    // Creates item-level reconciliation trace records.
    private static IReadOnlyList<ReconciliationItem> CreateReconciliationItems(
            long reconciliationBatchId,
            IReadOnlyCollection<TransactionEntry> entries)
    {
        return entries
            .Select(entry =>
            {
                decimal expectedAmount =entry.Amount;
                decimal actualAmount =entry.Amount;
                return new ReconciliationItem
                {
                    ReconciliationBatchId = reconciliationBatchId,
                    TransactionId = entry.TransactionId,
                    TransactionEntryId = entry.Id,
                    ExpectedAmount = expectedAmount,
                    ActualAmount = actualAmount,
                    Difference = actualAmount -expectedAmount,
                    Status = AuditConstants.ReconciliationMatchedStatus,
                    Note = "Matched during end-of-day internal reconciliation."
                };
            }).ToList();
    }

    // Gets the latest closing balance before the audit date.
    private async Task<decimal> GetOpeningBalanceAsync(
        long glAccountId,
        DateOnly auditDate,
        CancellationToken cancellationToken)
    {
        decimal? previousSummary =
            await _dbContext.DailySummaries
                .AsNoTracking()
                .Where(summary =>
                    summary.GlAccountId ==
                        glAccountId &&
                    summary.SummaryDate <
                        auditDate)
                .OrderByDescending(summary =>
                    summary.SummaryDate)
                .Select(summary => (decimal?)summary.ClosingBalance)
                .FirstOrDefaultAsync(
                    cancellationToken);

        return previousSummary
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
        return GlAccountBalanceCalculator.ApplyPeriodTotals(accountClass, openingBalance, totalDebit, totalCredit);
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

        List<long> glAccountIds =
            glAccountTotals
                .Select(total =>
                    total.GlAccountId)
                .Distinct()
                .ToList();

        Dictionary<long, GlAccount> glAccounts =
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

        List<DailySummary> summaries =
            new List<DailySummary>();

        foreach (GlAuditTotal total in glAccountTotals)
        {
            if (!glAccounts.TryGetValue(
                    total.GlAccountId,
                    out GlAccount? glAccount))
            {
                throw new NotFoundException(
                    MessageCode.ResourceNotFound);
            }

            decimal openingBalance =
                await GetOpeningBalanceAsync(
                    total.GlAccountId,
                    auditDate,
                    cancellationToken);

            decimal closingBalance =
                CalculateClosingBalance(
                    glAccount.AccountClass,
                    openingBalance,
                    total.TotalDebit,
                    total.TotalCredit);

            summaries.Add(new DailySummary
                {
                    SummaryDate = auditDate,
                    GlAccountId = total.GlAccountId,
                    OpeningBalance = openingBalance,
                    TotalDebit = total.TotalDebit,
                    TotalCredit = total.TotalCredit,
                    ClosingBalance = closingBalance,
                    GeneratedAt = DateTime.UtcNow
                });
        }

        return summaries;
    }
}
