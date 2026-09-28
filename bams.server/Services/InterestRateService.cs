using bams.server.Data;
using bams.server.DTO.Configuration;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Products;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class InterestRateService : IInterestRateService
{
    private readonly ApplicationDbContext _dbContext;

    public InterestRateService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// Gets all interest rate rules.
    public async Task<IReadOnlyList<InterestRateResponse>> GetInterestRatesAsync(
        CancellationToken cancellationToken)
    {
        var rules = await _dbContext.InterestRateRules
            .AsNoTracking()
            .Include(rule => rule.AccountType)
            .OrderBy(rule => rule.AccountTypeId)
            .ThenBy(rule => rule.EffectiveFrom)
            .ToListAsync(cancellationToken);

        return rules.Select(rule => rule.ToResponse()).ToList();
    }

    /// <summary>
    /// Gets the account types selectable in the interest rate form.
    /// </summary>
    public async Task<IReadOnlyList<AccountTypeOptionResponse>> GetAccountTypeOptionsAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.AccountTypes
            .AsNoTracking()
            .OrderBy(type => type.Code)
            .Select(type => new AccountTypeOptionResponse(type.Id, type.Code, type.Name))
            .ToListAsync(cancellationToken);
    }

    /// Validates the request and creates a new interest rate rule.
    public async Task<InterestRateResponse> CreateInterestRateAsync(
        CreateInterestRateRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(request.AccountTypeId, request.BalanceMin, request.BalanceMax,
            request.AnnualRate, request.EarlyWithdrawalRate, request.EffectiveFrom, request.EffectiveTo,
            request.Status, cancellationToken);

        var rule = new InterestRateRule
        {
            AccountTypeId = request.AccountTypeId,
            TermDays = request.TermDays,
            TermMonths = request.TermMonths,
            BalanceMin = request.BalanceMin,
            BalanceMax = request.BalanceMax,
            AnnualRate = request.AnnualRate,
            EarlyWithdrawalRate = request.EarlyWithdrawalRate,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = request.Status.Trim()
        };

        await _dbContext.InterestRateRules.AddAsync(rule, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        rule.AccountType = await _dbContext.AccountTypes
            .AsNoTracking()
            .FirstAsync(type => type.Id == rule.AccountTypeId, cancellationToken);

        return rule.ToResponse();
    }

    /// Validates the request and updates an existing interest rate rule.
    public async Task<InterestRateResponse> UpdateInterestRateAsync(
        long id,
        UpdateInterestRateRequest request,
        CancellationToken cancellationToken)
    {
        var rule = await _dbContext.InterestRateRules
            .FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);

        if (rule is null)
        {
            throw new NotFoundException(MessageCode.InterestRateRuleNotFound);
        }

        await ValidateAsync(request.AccountTypeId, request.BalanceMin, request.BalanceMax,
            request.AnnualRate, request.EarlyWithdrawalRate, request.EffectiveFrom, request.EffectiveTo,
            request.Status, cancellationToken);

        rule.AccountTypeId = request.AccountTypeId;
        rule.TermDays = request.TermDays;
        rule.TermMonths = request.TermMonths;
        rule.BalanceMin = request.BalanceMin;
        rule.BalanceMax = request.BalanceMax;
        rule.AnnualRate = request.AnnualRate;
        rule.EarlyWithdrawalRate = request.EarlyWithdrawalRate;
        rule.EffectiveFrom = request.EffectiveFrom;
        rule.EffectiveTo = request.EffectiveTo;
        rule.Status = request.Status.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        rule.AccountType = await _dbContext.AccountTypes
            .AsNoTracking()
            .FirstAsync(type => type.Id == rule.AccountTypeId, cancellationToken);

        return rule.ToResponse();
    }

    // Shared validation for create and update.
    private async Task ValidateAsync(
        long accountTypeId,
        decimal? balanceMin,
        decimal? balanceMax,
        decimal annualRate,
        decimal? earlyWithdrawalRate,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string status,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        }

        if (annualRate < 0 || earlyWithdrawalRate < 0)
        {
            throw new ValidationException(MessageCode.InvalidAmount);
        }

        if (balanceMin is not null && balanceMax is not null && balanceMin > balanceMax)
        {
            throw new ValidationException(MessageCode.InterestRateBalanceRangeInvalid);
        }

        if (effectiveTo is not null && effectiveTo < effectiveFrom)
        {
            throw new ValidationException(MessageCode.InvalidDateRange);
        }

        var accountTypeExists = await _dbContext.AccountTypes
            .AsNoTracking()
            .AnyAsync(type => type.Id == accountTypeId, cancellationToken);

        if (!accountTypeExists)
        {
            throw new NotFoundException(MessageCode.AccountTypeNotFound);
        }
    }
}
