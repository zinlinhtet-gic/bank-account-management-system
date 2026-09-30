using bams.server.Utils;
using System.Globalization;
using bams.server.Data;
using bams.server.Models.Accounts;
using bams.server.Models.Accounts.Enums;
using bams.server.Models.InterestFees;
using bams.server.Models.Products;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using bams.server.Services.Jobs;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Accrues monthly savings/fixed-deposit interest and credits eligible accruals after calendar quarters.</summary>
public sealed class InterestAccumulationService
{
    private const int AccountBatchSize = 250;
    private const string ActiveRuleStatus = "Active";
    // Annual rates are stored as percentages and fixed deposits accrue on an actual/365 day count.
    private const decimal PercentageDivisor = 100m;
    private const decimal DaysPerYear = 365m;
    private readonly ApplicationDbContext _dbContext;
    private readonly IAccountTypeService _accountTypeService;
    private readonly ScheduledFinancialPostingService _postingService;
    private readonly ILogger<InterestAccumulationService> _logger;

    public InterestAccumulationService(
        ApplicationDbContext dbContext,
        IAccountTypeService accountTypeService,
        ScheduledFinancialPostingService postingService,
        ILogger<InterestAccumulationService> logger)
    {
        _dbContext = dbContext;
        _accountTypeService = accountTypeService;
        _postingService = postingService;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        ScheduledJobExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Interest accumulation job started for scheduled run {ScheduledForUtc}.", executionContext.ScheduledForUtc);
        var runDate = ScheduledJobPeriod.GetRunDate(executionContext);
        var (periodStart, periodEnd) = ScheduledJobPeriod.GetPreviousCalendarMonth(runDate);
        var failures = new List<Exception>();

        await ProcessCategoryAsync(AccountTypeCategory.SAVING, runDate, periodStart, periodEnd, failures, cancellationToken);
        await ProcessCategoryAsync(AccountTypeCategory.FIXED, runDate, periodStart, periodEnd, failures, cancellationToken);
        if (failures.Count > 0)
        {
            throw new AggregateException($"Interest accumulation failed for {failures.Count} account(s).", failures);
        }
        _logger.LogInformation("Interest accumulation job completed successfully for run date {RunDate}.", runDate);
    }

    private async Task ProcessCategoryAsync(
        AccountTypeCategory category,
        DateOnly runDate,
        DateOnly periodStart,
        DateOnly periodEnd,
        List<Exception> failures,
        CancellationToken cancellationToken)
    {
        await foreach (var batch in _accountTypeService.GetAccountBatchesByCategoryAsync(
                           category, AccountBatchSize, cancellationToken))
        {
            foreach (var account in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (account.Status is not (AccountStatus.Active or AccountStatus.Dormant))
                {
                    continue;
                }

                try
                {
                    if (category == AccountTypeCategory.SAVING)
                    {
                        await AccrueSavingInterestAsync(account, periodStart, periodEnd, cancellationToken);
                        await PostQuarterlyInterestAsync(account.Id, category, runDate, periodEnd,
                            fixedDepositId: null, cancellationToken: cancellationToken);
                        continue;
                    }

                    var fixedDeposit = await GetAccruableFixedDepositAsync(
                        account.Id, periodStart, periodEnd, cancellationToken);
                    if (fixedDeposit is not null)
                    {
                        await AccrueFixedDepositInterestAsync(account, fixedDeposit, periodStart, periodEnd,
                            cancellationToken);
                    }

                    var fixedDepositIdForPosting = fixedDeposit is not null && fixedDeposit.MaturityDate >= periodEnd
                        ? fixedDeposit.Id
                        : (long?)null;
                    await PostQuarterlyInterestAsync(account.Id, category, runDate, periodEnd,
                        fixedDepositIdForPosting, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _dbContext.ChangeTracker.Clear();
                    _logger.LogError(exception, "Interest accumulation failed for account {AccountId}.", account.Id);
                    failures.Add(new InvalidOperationException(
                        $"Interest accumulation failed for account {account.Id}: {exception.Message}", exception));
                }
            }
        }
    }

    private async Task AccrueSavingInterestAsync(
        Account account,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        if (BusinessTime.ToBusinessDate(account.OpenedAt) > periodStart)
        {
            return;
        }

        var existing = await _dbContext.InterestAccruals.SingleOrDefaultAsync(accrual =>
                accrual.AccountId == account.Id && accrual.PeriodStart == periodStart &&
                accrual.PeriodEnd == periodEnd, cancellationToken);
        if (existing is not null)
        {
            await EnsureAccrualTransactionAsync(existing, cancellationToken);
            return;
        }

        var rateRule = await _dbContext.InterestRateRules.AsNoTracking()
            .Where(rule => rule.AccountTypeId == account.AccountTypeId &&
                rule.Status == ActiveRuleStatus && rule.EffectiveFrom <= periodEnd &&
                (!rule.EffectiveTo.HasValue || rule.EffectiveTo.Value >= periodEnd) &&
                !rule.TermMonths.HasValue && !rule.TermDays.HasValue &&
                (!rule.BalanceMin.HasValue || rule.BalanceMin.Value <= account.LedgerBalance) &&
                (!rule.BalanceMax.HasValue || rule.BalanceMax.Value >= account.LedgerBalance))
            .OrderByDescending(rule => rule.BalanceMin)
            .ThenByDescending(rule => rule.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        // A balance outside every configured tier earns no interest for the month. This is a data condition, not a
        // job failure: throwing here would fail (and retry) the whole run for every other account.
        if (rateRule is null)
        {
            _logger.LogWarning(
                "No effective savings interest rule applies to account {AccountId} for {Period}; no interest accrued.",
                account.Id,
                periodEnd.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            return;
        }

        var annualAmount = account.LedgerBalance * rateRule.AnnualRate / PercentageDivisor;
        var monthlyAmount = ScheduledJobPeriod.RoundMoney(annualAmount / 12m);
        var accrual = new InterestAccrual
        {
            AccountId = account.Id,
            InterestRateRuleId = rateRule.Id,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CalculationBalance = account.LedgerBalance,
            AnnualRate = rateRule.AnnualRate,
            CalculatedAmount = monthlyAmount,
            Status = "Accrued",
            CalculatedAt = DateTime.UtcNow
        };
        _dbContext.InterestAccruals.Add(accrual);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await EnsureAccrualTransactionAsync(accrual, cancellationToken);
    }

    private async Task<FixedDeposit?> GetAccruableFixedDepositAsync(
        long accountId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        var deposits = await _dbContext.FixedDeposits.AsNoTracking()
            .Where(deposit => deposit.AccountId == accountId &&
                deposit.Status == FixedDepositStatus.Active.ToString() &&
                deposit.StartDate <= periodEnd && deposit.MaturityDate >= periodStart)
            .OrderByDescending(deposit => deposit.StartDate)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (deposits.Count > 1)
        {
            throw new InvalidOperationException(
                $"Account {accountId} has multiple active fixed deposits; monthly interest cannot be accrued unambiguously.");
        }

        return deposits.SingleOrDefault();
    }

    private async Task AccrueFixedDepositInterestAsync(
        Account account,
        FixedDeposit fixedDeposit,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.InterestAccruals.SingleOrDefaultAsync(accrual =>
                accrual.AccountId == account.Id && accrual.PeriodStart == periodStart &&
                accrual.PeriodEnd == periodEnd, cancellationToken);
        if (existing is not null)
        {
            await EnsureAccrualTransactionAsync(existing, cancellationToken);
            return;
        }

        // A deposit earns interest from its start date up to, but not including, its maturity date, so a
        // 91-day term accrues exactly 91 days across the months it spans.
        var lastInterestDay = fixedDeposit.MaturityDate.AddDays(-1);
        var activeStart = fixedDeposit.StartDate > periodStart ? fixedDeposit.StartDate : periodStart;
        var activeEnd = lastInterestDay < periodEnd ? lastInterestDay : periodEnd;
        var activeDays = activeEnd.DayNumber - activeStart.DayNumber + 1;
        if (activeDays <= 0)
        {
            return;
        }

        // Round once on the period total; rounding the daily amount first over- or under-pays by up to half a
        // cent per day (about 10% on small deposits).
        var annualAmount = fixedDeposit.CurrentPrincipal * fixedDeposit.AppliedAnnualRate / PercentageDivisor;
        var totalInterest = ScheduledJobPeriod.RoundMoney(annualAmount * activeDays / DaysPerYear);
        var accrual = new InterestAccrual
        {
            AccountId = account.Id,
            InterestRateRuleId = fixedDeposit.InterestRateRuleId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CalculationBalance = fixedDeposit.CurrentPrincipal,
            AnnualRate = fixedDeposit.AppliedAnnualRate,
            CalculatedAmount = totalInterest,
            Status = "Accrued",
            CalculatedAt = DateTime.UtcNow
        };
        _dbContext.InterestAccruals.Add(accrual);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await EnsureAccrualTransactionAsync(accrual, cancellationToken);
    }

    private Task EnsureAccrualTransactionAsync(
        InterestAccrual accrual,
        CancellationToken cancellationToken)
    {
        if (accrual.CalculatedAmount <= 0m || accrual.AccruedTransactionId.HasValue || accrual.Status != "Accrued")
        {
            return Task.CompletedTask;
        }

        return _postingService.RecordAccrualAsync(
            accrual.AccountId,
            TransactionType.InterestAccrual,
            accrual.CalculatedAmount,
            "Monthly account interest accrued",
            accrual.PeriodEnd,
            interestAccrual: accrual,
            feeAccrual: null,
            cancellationToken: cancellationToken);
    }

    private async Task PostQuarterlyInterestAsync(
        long accountId,
        AccountTypeCategory category,
        DateOnly runDate,
        DateOnly periodEnd,
        long? fixedDepositId,
        CancellationToken cancellationToken)
    {
        if (periodEnd.Month % 3 != 0)
        {
            return;
        }

        var accruals = await _dbContext.InterestAccruals
            .Where(accrual => accrual.AccountId == accountId && accrual.Status == "Accrued" &&
                accrual.PeriodEnd <= periodEnd)
            .OrderBy(accrual => accrual.PeriodStart)
            .ToListAsync(cancellationToken);
        // Include older unpaid accruals if an earlier quarter's posting could not complete.
        foreach (var accrual in accruals)
        {
            await EnsureAccrualTransactionAsync(accrual, cancellationToken);
        }

        var amount = ScheduledJobPeriod.RoundMoney(accruals.Sum(accrual => accrual.CalculatedAmount));
        if (amount <= 0m)
        {
            foreach (var accrual in accruals)
            {
                accrual.Status = "Posted";
                accrual.PostedAt = DateTime.UtcNow;
            }

            if (accruals.Count > 0)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var transactionId = await _postingService.PostAsync(
            accountId,
            TransactionType.InterestCredit,
            amount,
            category == AccountTypeCategory.SAVING
                ? "Quarterly savings interest credit"
                : "Quarterly fixed-deposit interest credit",
            runDate,
            interestAccruals: accruals,
            feeAccruals: null,
            fixedDepositId: fixedDepositId,
            cancellationToken: cancellationToken);
        if (transactionId == 0)
        {
            // A credit cannot exceed available balance; zero indicates invalid configuration or account state.
            throw new InvalidOperationException($"Interest credit for account {accountId} was not posted.");
        }
    }
}
