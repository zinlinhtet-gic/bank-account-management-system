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

/// <summary>Accrues saving maintenance fees monthly, deducts them quarterly, and deducts dormant penalties monthly.</summary>
public sealed class AccountMaintenanceService
{
    private const int AccountBatchSize = 250;
    private const string ActiveRuleStatus = "Active";

    private readonly ApplicationDbContext _dbContext;
    private readonly IAccountTypeService _accountTypeService;
    private readonly ScheduledFinancialPostingService _postingService;
    private readonly ILogger<AccountMaintenanceService> _logger;

    public AccountMaintenanceService(
        ApplicationDbContext dbContext,
        IAccountTypeService accountTypeService,
        ScheduledFinancialPostingService postingService,
        ILogger<AccountMaintenanceService> logger)
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
        _logger.LogInformation("Account maintenance job started for scheduled run {ScheduledForUtc}.", executionContext.ScheduledForUtc);
        var runDate = ScheduledJobPeriod.GetRunDate(executionContext);
        var (periodStart, periodEnd) = ScheduledJobPeriod.GetPreviousCalendarMonth(runDate);
        var failures = new List<Exception>();

        await foreach (var batch in _accountTypeService.GetAccountBatchesByCategoryAsync(
                           AccountTypeCategory.SAVING, AccountBatchSize, cancellationToken))
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
                    await MaintainAccountAsync(account, runDate, periodStart, periodEnd, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _dbContext.ChangeTracker.Clear();
                    _logger.LogError(exception, "Maintenance failed for account {AccountId}.", account.Id);
                    failures.Add(new InvalidOperationException(
                        $"Maintenance failed for account {account.Id}: {exception.Message}", exception));
                }
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException($"Maintenance failed for {failures.Count} account(s).", failures);
        }
        _logger.LogInformation("Account maintenance job completed successfully for run date {RunDate}.", runDate);
    }

    private async Task MaintainAccountAsync(
        Account account,
        DateOnly runDate,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        if (DateOnly.FromDateTime(account.OpenedAt) > periodEnd)
        {
            return;
        }

        var maintenanceRule = await GetApplicableFeeRuleAsync(
            account.AccountTypeId, FeeType.Maintenance, periodEnd, cancellationToken);
        await EnsureMonthlyAccrualAsync(
            account, maintenanceRule, periodStart, periodEnd, cancellationToken);

        if (account.Status == AccountStatus.Dormant)
        {
            var penaltyRule = await GetApplicableFeeRuleAsync(
                account.AccountTypeId, FeeType.DormantAccount, periodEnd, cancellationToken);
            await EnsureMonthlyAccrualAsync(account, penaltyRule, periodStart, periodEnd, cancellationToken);

            var outstandingPenalties = await _dbContext.FeeAccruals
                .Where(accrual => accrual.AccountId == account.Id &&
                    accrual.FeeType == FeeType.DormantAccount &&
                    accrual.Status == FeeAccrualStatus.Accrued && accrual.PeriodEnd <= periodEnd)
                .ToListAsync(cancellationToken);
            foreach (var accrual in outstandingPenalties)
            {
                await EnsureAccrualTransactionAsync(accrual, cancellationToken);
            }

            var penaltyAmount = ScheduledJobPeriod.RoundMoney(outstandingPenalties.Sum(accrual => accrual.Amount));
            if (penaltyAmount > 0m)
            {
                var transactionId = await _postingService.PostAsync(
                    account.Id,
                    TransactionType.Penalty,
                    penaltyAmount,
                    "Monthly dormant account penalty",
                    runDate,
                    interestAccruals: null,
                    feeAccruals: outstandingPenalties,
                    fixedDepositId: null,
                    cancellationToken: cancellationToken);
                if (transactionId == 0)
                {
                    _logger.LogWarning("Dormant penalty for account {AccountId} remains accrued because its balance is insufficient.", account.Id);
                }
            }
        }

        if (periodEnd.Month % 3 != 0)
        {
            return;
        }

        var quarterStart = ScheduledJobPeriod.GetCalendarQuarterStart(periodEnd);
        var outstandingMaintenance = await _dbContext.FeeAccruals
            .Where(accrual => accrual.AccountId == account.Id &&
                accrual.FeeType == FeeType.Maintenance &&
                accrual.Status == FeeAccrualStatus.Accrued &&
                accrual.PeriodStart >= quarterStart && accrual.PeriodEnd <= periodEnd)
            .ToListAsync(cancellationToken);

        // Bring forward older unpaid quarterly charges so a later run can settle them after funding.
        var overdueMaintenance = await _dbContext.FeeAccruals
            .Where(accrual => accrual.AccountId == account.Id &&
                accrual.FeeType == FeeType.Maintenance &&
                accrual.Status == FeeAccrualStatus.Accrued && accrual.PeriodEnd < quarterStart)
            .ToListAsync(cancellationToken);
        outstandingMaintenance.AddRange(overdueMaintenance);
        foreach (var accrual in outstandingMaintenance)
        {
            await EnsureAccrualTransactionAsync(accrual, cancellationToken);
        }

        var maintenanceAmount = ScheduledJobPeriod.RoundMoney(
            outstandingMaintenance.Sum(accrual => accrual.Amount));
        if (maintenanceAmount <= 0m)
        {
            return;
        }

        var maintenanceTransactionId = await _postingService.PostAsync(
            account.Id,
            TransactionType.MaintenanceFee,
            maintenanceAmount,
            "Quarterly saving account maintenance fee",
            runDate,
            interestAccruals: null,
            feeAccruals: outstandingMaintenance,
            fixedDepositId: null,
            cancellationToken: cancellationToken);
        if (maintenanceTransactionId == 0)
        {
            _logger.LogWarning("Maintenance fee for account {AccountId} remains accrued because its balance is insufficient.", account.Id);
        }
    }

    private async Task<FeeRule> GetApplicableFeeRuleAsync(
        long accountTypeId,
        FeeType feeType,
        DateOnly effectiveDate,
        CancellationToken cancellationToken)
    {
        var rule = await _dbContext.FeeRules.AsNoTracking()
            .Where(item => item.AccountTypeId == accountTypeId && item.FeeType == feeType &&
                item.Status == ActiveRuleStatus && item.EffectiveFrom <= effectiveDate &&
                (!item.EffectiveTo.HasValue || item.EffectiveTo.Value >= effectiveDate))
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (rule is null || !rule.Amount.HasValue || rule.Amount.Value <= 0m || rule.Percentage.HasValue)
        {
            throw new InvalidOperationException(
                $"No valid fixed-amount {feeType} rule applies to saving account type {accountTypeId} on {effectiveDate:yyyy-MM-dd}.");
        }

        return rule;
    }

    private async Task<FeeAccrual> EnsureMonthlyAccrualAsync(
        Account account,
        FeeRule rule,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.FeeAccruals.SingleOrDefaultAsync(accrual =>
            accrual.AccountId == account.Id && accrual.FeeType == rule.FeeType &&
            accrual.PeriodStart == periodStart && accrual.PeriodEnd == periodEnd,
            cancellationToken);
        if (existing is not null)
        {
            await EnsureAccrualTransactionAsync(existing, cancellationToken);
            return existing;
        }

        var accrual = new FeeAccrual
        {
            AccountId = account.Id,
            FeeRuleId = rule.Id,
            FeeType = rule.FeeType,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Amount = ScheduledJobPeriod.RoundMoney(rule.Amount!.Value),
            TaxAmount = 0m,
            Status = FeeAccrualStatus.Accrued,
            CalculatedAt = DateTime.UtcNow
        };
        _dbContext.FeeAccruals.Add(accrual);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await EnsureAccrualTransactionAsync(accrual, cancellationToken);
        return accrual;
    }

    private Task EnsureAccrualTransactionAsync(
        FeeAccrual accrual,
        CancellationToken cancellationToken)
    {
        if (accrual.Amount <= 0m || accrual.AccruedTransactionId.HasValue ||
            accrual.Status != FeeAccrualStatus.Accrued)
        {
            return Task.CompletedTask;
        }

        var transactionType = accrual.FeeType switch
        {
            FeeType.Maintenance => TransactionType.MaintenanceAccrual,
            FeeType.DormantAccount => TransactionType.DormantPenaltyAccrual,
            _ => throw new InvalidOperationException($"Fee type '{accrual.FeeType}' cannot be accrued by account maintenance.")
        };
        return _postingService.RecordAccrualAsync(
            accrual.AccountId,
            transactionType,
            accrual.Amount,
            accrual.FeeType == FeeType.Maintenance
                ? "Monthly saving account maintenance fee accrued"
                : "Monthly dormant account penalty accrued",
            accrual.PeriodEnd,
            interestAccrual: null,
            feeAccrual: accrual,
            cancellationToken: cancellationToken);
    }
}
