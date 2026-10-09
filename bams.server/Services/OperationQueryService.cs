using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Operations;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts.Enums;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// Read-only Operations queries: lists each customer account's interest accruals, fees and fixed-deposit
/// maturity details. Nothing here changes data. The customer shown on a row is the account's primary holder.
/// </summary>
public sealed class OperationQueryService : IOperationQueryService
{
    private readonly ApplicationDbContext _dbContext;

    public OperationQueryService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lists interest accruals matching the filters, newest period first, one page at a time.
    /// </summary>
    public async Task<PagedResponse<InterestOperationResponse>> GetInterestOperationsAsync(
        InterestOperationQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = TransactionRequestValidator.ResolvePaging(query.Page, query.PageSize);
        ValidateDateRange(query.From, query.To);

        var accruals = _dbContext.InterestAccruals.AsNoTracking();

        // Each filter is optional; only the ones provided narrow the list.
        if (TransactionRequestValidator.TrimToNull(query.Search) is { } search)
        {
            var accountIds = GetAccountIdsMatching(search);
            accruals = accruals.Where(accrual => accountIds.Contains(accrual.AccountId));
        }

        if (TransactionRequestValidator.TrimToNull(query.Status) is { } status)
        {
            accruals = accruals.Where(accrual => accrual.Status == status);
        }

        if (query.From is { } from)
        {
            accruals = accruals.Where(accrual => accrual.PeriodEnd >= from);
        }

        if (query.To is { } to)
        {
            accruals = accruals.Where(accrual => accrual.PeriodEnd <= to);
        }

        var totalCount = await accruals.CountAsync(cancellationToken);
        var items = await accruals
            .OrderByDescending(accrual => accrual.PeriodEnd)
            .ThenByDescending(accrual => accrual.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(accrual => new InterestOperationResponse(
                accrual.Id,
                accrual.AccountId,
                accrual.Account!.AccountNo,
                accrual.Account.AccountType!.Code,
                accrual.Account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.CustomerNo).FirstOrDefault(),
                accrual.Account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.FullName).FirstOrDefault(),
                accrual.PeriodStart,
                accrual.PeriodEnd,
                accrual.CalculationBalance,
                accrual.AnnualRate,
                accrual.CalculatedAmount,
                accrual.Status,
                accrual.CalculatedAt,
                accrual.PostedAt))
            .ToListAsync(cancellationToken);

        return CreatePage(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Lists fees (maintenance, early withdrawal, dormant penalty, ...) matching the filters, newest period first.
    /// </summary>
    public async Task<PagedResponse<FeeOperationResponse>> GetFeeOperationsAsync(
        FeeOperationQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = TransactionRequestValidator.ResolvePaging(query.Page, query.PageSize);
        ValidateDateRange(query.From, query.To);

        var fees = _dbContext.FeeAccruals.AsNoTracking();

        if (TransactionRequestValidator.TrimToNull(query.Search) is { } search)
        {
            var accountIds = GetAccountIdsMatching(search);
            fees = fees.Where(fee => accountIds.Contains(fee.AccountId));
        }

        if (query.FeeType is { } feeType)
        {
            fees = fees.Where(fee => fee.FeeType == feeType);
        }

        if (query.Status is { } status)
        {
            fees = fees.Where(fee => fee.Status == status);
        }

        if (query.From is { } from)
        {
            fees = fees.Where(fee => fee.PeriodEnd >= from);
        }

        if (query.To is { } to)
        {
            fees = fees.Where(fee => fee.PeriodEnd <= to);
        }

        var totalCount = await fees.CountAsync(cancellationToken);
        var items = await fees
            .OrderByDescending(fee => fee.PeriodEnd)
            .ThenByDescending(fee => fee.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(fee => new FeeOperationResponse(
                fee.Id,
                fee.AccountId,
                fee.Account!.AccountNo,
                fee.Account.AccountType!.Code,
                fee.Account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.CustomerNo).FirstOrDefault(),
                fee.Account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.FullName).FirstOrDefault(),
                fee.FeeType,
                fee.PeriodStart,
                fee.PeriodEnd,
                fee.Amount,
                fee.TaxAmount,
                fee.Status,
                fee.CalculatedAt,
                fee.PostedAt))
            .ToListAsync(cancellationToken);

        return CreatePage(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Lists fixed deposits matching the filters, nearest maturity date first, with accrued and expected interest.
    /// </summary>
    public async Task<PagedResponse<FixedDepositMaturityResponse>> GetFixedDepositMaturitiesAsync(
        FixedDepositMaturityQuery query,
        CancellationToken cancellationToken)
    {
        var (page, pageSize) = TransactionRequestValidator.ResolvePaging(query.Page, query.PageSize);
        ValidateDateRange(query.From, query.To);

        var deposits = _dbContext.FixedDeposits.AsNoTracking();

        if (TransactionRequestValidator.TrimToNull(query.Search) is { } search)
        {
            var accountIds = GetAccountIdsMatching(search);
            deposits = deposits.Where(deposit => accountIds.Contains(deposit.AccountId));
        }

        // Fixed deposit status is stored as the enum name.
        if (query.Status is { } status)
        {
            var statusName = status.ToString();
            deposits = deposits.Where(deposit => deposit.Status == statusName);
        }

        if (query.From is { } from)
        {
            deposits = deposits.Where(deposit => deposit.MaturityDate >= from);
        }

        if (query.To is { } to)
        {
            deposits = deposits.Where(deposit => deposit.MaturityDate <= to);
        }

        var totalCount = await deposits.CountAsync(cancellationToken);
        var rows = await deposits
            .OrderBy(deposit => deposit.MaturityDate)
            .ThenBy(deposit => deposit.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(deposit => new
            {
                deposit.Id,
                deposit.AccountId,
                deposit.Account!.AccountNo,
                AccountTypeCode = deposit.Account.AccountType!.Code,
                CustomerNo = deposit.Account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.CustomerNo).FirstOrDefault(),
                CustomerName = deposit.Account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.FullName).FirstOrDefault(),
                deposit.CurrentPrincipal,
                deposit.AppliedAnnualRate,
                deposit.StartDate,
                deposit.MaturityDate,
                // A renewed deposit keeps its account, so only accruals inside this deposit's term count.
                InterestAccrued = _dbContext.InterestAccruals
                    .Where(accrual => accrual.AccountId == deposit.AccountId &&
                        accrual.PeriodEnd >= deposit.StartDate && accrual.PeriodStart < deposit.MaturityDate)
                    .Sum(accrual => (decimal?)accrual.CalculatedAmount) ?? 0m,
                deposit.RenewalInstruction,
                PayoutAccountNo = deposit.PayoutAccount != null ? deposit.PayoutAccount.AccountNo : null,
                deposit.Status
            })
            .ToListAsync(cancellationToken);

        var today = BusinessTime.Today;
        var items = rows
            .Select(row => new FixedDepositMaturityResponse(
                row.Id,
                row.AccountId,
                row.AccountNo,
                row.AccountTypeCode,
                row.CustomerNo,
                row.CustomerName,
                row.CurrentPrincipal,
                row.AppliedAnnualRate,
                row.StartDate,
                row.MaturityDate,
                row.MaturityDate.DayNumber - today.DayNumber,
                row.InterestAccrued,
                CalculateTermInterest(row.CurrentPrincipal, row.AppliedAnnualRate, row.StartDate, row.MaturityDate),
                row.RenewalInstruction,
                row.PayoutAccountNo,
                Enum.Parse<FixedDepositStatus>(row.Status)))
            .ToList();

        return CreatePage(items, page, pageSize, totalCount);
    }

    /// <summary>
    /// Gets one interest accrual with its account, rule and the accrual / credit transactions.
    /// </summary>
    public async Task<InterestOperationDetailResponse> GetInterestOperationByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var accrual = await _dbContext.InterestAccruals
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.AccountId,
                item.InterestRateRuleId,
                item.PeriodStart,
                item.PeriodEnd,
                item.CalculationBalance,
                item.AnnualRate,
                item.CalculatedAmount,
                item.Status,
                item.CalculatedAt,
                item.PostedAt,
                item.AccruedTransactionId,
                item.PostedTransactionId
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(MessageCode.InterestAccrualNotFound);

        var account = await GetAccountDetailAsync(accrual.AccountId, cancellationToken);
        var accruedTransaction = await GetTransactionLinkAsync(accrual.AccruedTransactionId, cancellationToken);
        var postedTransaction = await GetTransactionLinkAsync(accrual.PostedTransactionId, cancellationToken);

        return new InterestOperationDetailResponse(
            accrual.Id,
            account,
            accrual.InterestRateRuleId,
            accrual.PeriodStart,
            accrual.PeriodEnd,
            accrual.CalculationBalance,
            accrual.AnnualRate,
            accrual.CalculatedAmount,
            accrual.Status,
            accrual.CalculatedAt,
            accrual.PostedAt,
            accruedTransaction,
            postedTransaction);
    }

    /// <summary>
    /// Gets one fee accrual with its account, fee rule and the accrual / deduction transactions.
    /// </summary>
    public async Task<FeeOperationDetailResponse> GetFeeOperationByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var fee = await _dbContext.FeeAccruals
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.AccountId,
                item.FeeType,
                item.FeeRuleId,
                RuleAmount = item.FeeRule!.Amount,
                RulePercentage = item.FeeRule.Percentage,
                item.PeriodStart,
                item.PeriodEnd,
                item.Amount,
                item.TaxAmount,
                item.Status,
                item.CalculatedAt,
                item.PostedAt,
                item.AccruedTransactionId,
                item.PostedTransactionId
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(MessageCode.FeeAccrualNotFound);

        var account = await GetAccountDetailAsync(fee.AccountId, cancellationToken);
        var accruedTransaction = await GetTransactionLinkAsync(fee.AccruedTransactionId, cancellationToken);
        var postedTransaction = await GetTransactionLinkAsync(fee.PostedTransactionId, cancellationToken);

        return new FeeOperationDetailResponse(
            fee.Id,
            account,
            fee.FeeType,
            fee.FeeRuleId,
            fee.RuleAmount,
            fee.RulePercentage,
            fee.PeriodStart,
            fee.PeriodEnd,
            fee.Amount,
            fee.TaxAmount,
            fee.Status,
            fee.CalculatedAt,
            fee.PostedAt,
            accruedTransaction,
            postedTransaction);
    }

    /// <summary>
    /// Gets one fixed deposit with its account, principal history and month-by-month interest schedule.
    /// </summary>
    public async Task<FixedDepositMaturityDetailResponse> GetFixedDepositMaturityByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var deposit = await _dbContext.FixedDeposits
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.AccountId,
                item.OriginalPrincipal,
                item.CurrentPrincipal,
                item.AppliedAnnualRate,
                item.TermDays,
                item.TermMonths,
                item.StartDate,
                item.MaturityDate,
                item.RenewalInstruction,
                PayoutAccountNo = item.PayoutAccount != null ? item.PayoutAccount.AccountNo : null,
                item.Status,
                item.CreatedAt
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(MessageCode.FixedDepositNotFound);

        var account = await GetAccountDetailAsync(deposit.AccountId, cancellationToken);

        // A renewed deposit keeps its account, so only accruals inside this deposit's term belong to it.
        var schedule = await _dbContext.InterestAccruals
            .AsNoTracking()
            .Where(accrual => accrual.AccountId == deposit.AccountId &&
                accrual.PeriodEnd >= deposit.StartDate && accrual.PeriodStart < deposit.MaturityDate)
            .OrderBy(accrual => accrual.PeriodStart)
            .Select(accrual => new FixedDepositInterestLine(
                accrual.Id,
                accrual.PeriodStart,
                accrual.PeriodEnd,
                accrual.CalculatedAmount,
                accrual.Status,
                accrual.PostedAt))
            .ToListAsync(cancellationToken);

        var interestPosted = schedule
            .Where(line => line.Status == OperationConstants.InterestPostedStatus)
            .Sum(line => line.Amount);

        return new FixedDepositMaturityDetailResponse(
            deposit.Id,
            account,
            deposit.OriginalPrincipal,
            deposit.CurrentPrincipal,
            deposit.AppliedAnnualRate,
            deposit.TermDays,
            deposit.TermMonths,
            deposit.StartDate,
            deposit.MaturityDate,
            deposit.MaturityDate.DayNumber - BusinessTime.Today.DayNumber,
            schedule.Sum(line => line.Amount),
            interestPosted,
            CalculateTermInterest(deposit.CurrentPrincipal, deposit.AppliedAnnualRate, deposit.StartDate,
                deposit.MaturityDate),
            deposit.RenewalInstruction,
            deposit.PayoutAccountNo,
            Enum.Parse<FixedDepositStatus>(deposit.Status),
            deposit.CreatedAt,
            schedule);
    }

    // Loads the account header shown on every detail card, with the primary holder's contact details.
    private async Task<OperationAccountDetail> GetAccountDetailAsync(long accountId, CancellationToken cancellationToken)
    {
        return await _dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => new OperationAccountDetail(
                account.Id,
                account.AccountNo,
                account.AccountType!.Code,
                account.AccountType.Name,
                account.Status,
                account.LedgerBalance,
                account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.CustomerNo).FirstOrDefault(),
                account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.FullName).FirstOrDefault(),
                account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.NrcNumber).FirstOrDefault(),
                account.AccountHolders.Where(holder => holder.IsPrimary)
                    .Select(holder => holder.Customer!.Phone).FirstOrDefault()))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(MessageCode.AccountNotFound);
    }

    // Loads the number, amount and time of a linked transaction; null when the record has no such transaction yet.
    private async Task<OperationTransactionLink?> GetTransactionLinkAsync(
        long? transactionId,
        CancellationToken cancellationToken)
    {
        if (transactionId is not { } id)
        {
            return null;
        }

        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.Id == id)
            .Select(transaction => new OperationTransactionLink(
                transaction.Id,
                transaction.TransactionNo,
                transaction.Amount,
                transaction.TransactionAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    // Ids of accounts whose number, or any holder's customer number or name, contains the search text.
    private IQueryable<long> GetAccountIdsMatching(string search)
    {
        return _dbContext.Accounts
            .Where(account => account.AccountNo.Contains(search) ||
                account.AccountHolders.Any(holder =>
                    holder.Customer!.CustomerNo.Contains(search) || holder.Customer.FullName.Contains(search)))
            .Select(account => account.Id);
    }

    // A deposit earns interest from its start date up to, but not including, its maturity date (actual/365),
    // matching InterestAccumulationService.
    private static decimal CalculateTermInterest(
        decimal principal,
        decimal annualRate,
        DateOnly startDate,
        DateOnly maturityDate)
    {
        var termDays = maturityDate.DayNumber - startDate.DayNumber;
        var annualAmount = principal * annualRate / OperationConstants.PercentageDivisor;

        return ScheduledJobPeriod.RoundMoney(annualAmount * termDays / OperationConstants.DaysPerYear);
    }

    // Rejects a date filter whose start is after its end.
    private static void ValidateDateRange(DateOnly? from, DateOnly? to)
    {
        if (from is { } start && to is { } end && start > end)
        {
            throw new ValidationException(MessageCode.InvalidDateRange);
        }
    }

    private static PagedResponse<T> CreatePage<T>(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        return new PagedResponse<T>(items, page, pageSize, totalCount, totalPages);
    }
}
