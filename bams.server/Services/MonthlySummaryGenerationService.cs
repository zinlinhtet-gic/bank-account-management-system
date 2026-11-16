using bams.server.Constants;
using bams.server.Data;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Builds one monthly GL snapshot from every journal row posted in the selected month.</summary>
public sealed class MonthlySummaryGenerationService(ApplicationDbContext db)
{
    public async Task EnsurePreviousMonthClosedAsync(DateTime scheduledForUtc, CancellationToken cancellationToken)
    {
        var runDate = BusinessTime.ToBusinessDate(scheduledForUtc);
        var (periodStart, periodEnd) = ScheduledJobPeriod.GetPreviousCalendarMonth(runDate);
        // var periodEndIsClosed = await db.BusinessDates.AsNoTracking().AnyAsync(item =>
        //     item.Date == periodEnd && item.Status == OperationsConstants.BusinessDateClosed, cancellationToken);
        if (await HasUnclosedBusinessDateAsync(periodStart, periodEnd, cancellationToken))
            throw new BusinessRuleException(MessageCode.MonthlyAccountingPeriodNotClosed);
    }

    // True when any business date inside the period is not Closed yet (same rule the monthly close job enforces).
    private Task<bool> HasUnclosedBusinessDateAsync(DateOnly periodStart, DateOnly periodEnd, CancellationToken cancellationToken) =>
        db.BusinessDates.AnyAsync(item => item.Date >= periodStart && item.Date <= periodEnd && item.Status != OperationsConstants.BusinessDateClosed, cancellationToken);

    /// <summary>
    /// Fills in monthly summaries that are missing, oldest first, from the month of the very first accounting entry
    /// up to the previous calendar month (the current month is never summarised until it ends). Months that already
    /// have summaries are left untouched, so a month missing in the middle of the history is also built. Generation
    /// stops at the first month whose business dates are not all Closed, because every month's opening balance
    /// comes from the month before it.
    /// </summary>
    /// <returns>How many months were generated.</returns>
    public async Task<int> BackfillMissingMonthsAsync(DateOnly today, ILogger logger, CancellationToken cancellationToken)
    {
        // The first entry decides where history starts; with no entries there is nothing to summarise.
        var firstPostingDate = await db.TransactionEntries.AsNoTracking()
            .MinAsync(entry => (DateOnly?)entry.PostingDate, cancellationToken);
        if (firstPostingDate is null)
        {
            logger.LogInformation("Monthly summary backfill skipped: no accounting entries exist.");
            return 0;
        }

        // Months that already have summaries must not be rebuilt, so load them once as (year, month) pairs.
        var summarisedMonths = (await db.MonthlySummaries.AsNoTracking()
            .Select(item => new { item.Year, item.Month })
            .Distinct()
            .ToListAsync(cancellationToken))
            .Select(item => (item.Year, item.Month))
            .ToHashSet();

        var (_, lastMonthEnd) = ScheduledJobPeriod.GetPreviousCalendarMonth(today);
        var generatedCount = 0;
        for (var monthStart = new DateOnly(firstPostingDate.Value.Year, firstPostingDate.Value.Month, 1);
             monthStart <= lastMonthEnd;
             monthStart = monthStart.AddMonths(1))
        {
            if (summarisedMonths.Contains((monthStart.Year, monthStart.Month)))
            {
                continue;
            }

            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            if (await HasUnclosedBusinessDateAsync(monthStart, monthEnd, cancellationToken))
            {
                logger.LogWarning(
                    "Monthly summary backfill stopped at {Year}-{Month:00}: its business dates are not all closed. Close them through End of Day, then restart or retry the monthly close.",
                    monthStart.Year, monthStart.Month);
                break;
            }

            // GeneratePreviousMonthAsync summarises the month before its run date, so pass the first day of the next month.
            await GeneratePreviousMonthAsync(monthStart.AddMonths(1), cancellationToken);
            generatedCount++;
            logger.LogInformation("Monthly summary generated for {Year}-{Month:00}.", monthStart.Year, monthStart.Month);
        }

        return generatedCount;
    }

    public async Task GeneratePreviousMonthAsync(DateOnly runDate, CancellationToken cancellationToken)
    {
        var (periodStart, periodEnd) = ScheduledJobPeriod.GetPreviousCalendarMonth(runDate);
        var year = periodStart.Year;
        var month = periodStart.Month;
        var generatedAt = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var accounts = await db.GlAccounts.AsNoTracking().OrderBy(item => item.Id).ToListAsync(cancellationToken);
        var monthTotals = await db.TransactionEntries.AsNoTracking()
            .Where(entry => entry.PostingDate >= periodStart && entry.PostingDate <= periodEnd)
            .GroupBy(entry => entry.GlAccountId)
            .Select(group => new
            {
                GlAccountId = group.Key,
                Debit = group.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount),
                Credit = group.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount)
            }).ToDictionaryAsync(item => item.GlAccountId, cancellationToken);

        var priorMonthly = await db.MonthlySummaries.AsNoTracking()
            .Where(item => item.Year < year || (item.Year == year && item.Month < month))
            .OrderByDescending(item => item.Year).ThenByDescending(item => item.Month)
            .ToListAsync(cancellationToken);
        var monthlyClosingByAccount = priorMonthly.GroupBy(item => item.GlAccountId)
            .ToDictionary(group => group.Key, group => group.First().ClosingBalance);
        var lastMonthlyDateByAccount = priorMonthly.GroupBy(item => item.GlAccountId)
            .ToDictionary(group => group.Key, group => new DateOnly(group.First().Year, group.First().Month,
                DateTime.DaysInMonth(group.First().Year, group.First().Month)));

        var priorDaily = await db.DailySummaries.AsNoTracking().Where(item => item.SummaryDate < periodStart)
            .OrderByDescending(item => item.SummaryDate).ToListAsync(cancellationToken);
        var latestDailyByAccount = priorDaily.GroupBy(item => item.GlAccountId)
            .ToDictionary(group => group.Key, group => group.First());

        // If the database predates both snapshot tables for an account, derive its opening position from the
        // raw journal instead of treating missing historical data as a zero balance.
        var accountsWithoutSnapshots = accounts.Where(account =>
            !monthlyClosingByAccount.ContainsKey(account.Id) && !latestDailyByAccount.ContainsKey(account.Id))
            .Select(account => account.Id).ToHashSet();
        var rawOpeningTotals = accountsWithoutSnapshots.Count == 0
            ? new Dictionary<long, (decimal Debit, decimal Credit)>()
            : await db.TransactionEntries.AsNoTracking()
                .Where(entry => accountsWithoutSnapshots.Contains(entry.GlAccountId) && entry.PostingDate < periodStart)
                .GroupBy(entry => entry.GlAccountId)
                .Select(group => new
                {
                    GlAccountId = group.Key,
                    Debit = group.Where(entry => entry.EntryType == EntryType.Debit).Sum(entry => entry.Amount),
                    Credit = group.Where(entry => entry.EntryType == EntryType.Credit).Sum(entry => entry.Amount)
                })
                .ToDictionaryAsync(item => item.GlAccountId, item => (item.Debit, item.Credit), cancellationToken);

        var existing = await db.MonthlySummaries.Where(item => item.Year == year && item.Month == month)
            .ToDictionaryAsync(item => item.GlAccountId, cancellationToken);

        foreach (var account in accounts)
        {
            var hasDailyAfterMonthly = latestDailyByAccount.TryGetValue(account.Id, out var daily) &&
                (!lastMonthlyDateByAccount.TryGetValue(account.Id, out var monthlyDate) || daily.SummaryDate > monthlyDate);
            var openingBalance = hasDailyAfterMonthly
                ? daily!.ClosingBalance
                : monthlyClosingByAccount.TryGetValue(account.Id, out var monthlyClosing)
                    ? monthlyClosing
                    : CalculateOpeningFromJournal(account.AccountClass, rawOpeningTotals.GetValueOrDefault(account.Id));
            var debit = monthTotals.GetValueOrDefault(account.Id)?.Debit ?? 0m;
            var credit = monthTotals.GetValueOrDefault(account.Id)?.Credit ?? 0m;
            var closingBalance = GlAccountBalanceCalculator.ApplyPeriodTotals(account.AccountClass, openingBalance, debit, credit);

            if (!existing.TryGetValue(account.Id, out var summary))
            {
                summary = new MonthlySummary { Year = year, Month = month, GlAccountId = account.Id };
                db.MonthlySummaries.Add(summary);
            }

            summary.OpeningBalance = openingBalance;
            summary.TotalDebit = debit;
            summary.TotalCredit = credit;
            summary.ClosingBalance = closingBalance;
            summary.GeneratedAt = generatedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static decimal CalculateOpeningFromJournal(GlAccountClass accountClass, (decimal Debit, decimal Credit) totals) =>
        GlAccountBalanceCalculator.ApplyPeriodTotals(accountClass, 0m, totals.Debit, totals.Credit);
}
