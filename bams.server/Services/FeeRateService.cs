using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Configuration;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Products;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class FeeRateService : IFeeRateService
{
    private const int PageSize = 10;

    private readonly ApplicationDbContext _dbContext;

    public FeeRateService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// Gets one page of fee rules, 10 per page.
    public async Task<PagedResponse<FeeRuleResponse>> GetFeeRulesAsync(
        int page,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);

        var query = _dbContext.FeeRules
            .AsNoTracking()
            .Include(rule => rule.AccountType)
            .OrderBy(rule => rule.AccountTypeId)
            .ThenBy(rule => rule.EffectiveFrom);

        var totalCount = await query.CountAsync(cancellationToken);
        var rules = await query
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<FeeRuleResponse>(
            rules.Select(rule => rule.ToResponse()).ToList(),
            page,
            PageSize,
            totalCount);
    }

    /// Gets the account types selectable in the fee rule form.
    public async Task<IReadOnlyList<AccountTypeOptionResponse>> GetAccountTypeOptionsAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.AccountTypes
            .AsNoTracking()
            .OrderBy(type => type.Code)
            .Select(type => new AccountTypeOptionResponse(type.Id, type.Code, type.Name))
            .ToListAsync(cancellationToken);
    }

    /// Validates the request and creates a new fee rule.
    public async Task<FeeRuleResponse> CreateFeeRuleAsync(
        CreateFeeRuleRequest request,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(request.AccountTypeId, request.FeeType, request.Amount, request.Percentage,
            request.MinimumFee, request.MaximumFee, request.TaxRate, request.EffectiveFrom, request.EffectiveTo,
            request.Status, cancellationToken);

        var rule = new FeeRule
        {
            AccountTypeId = request.AccountTypeId,
            FeeType = request.FeeType,
            Amount = request.Amount,
            Percentage = request.Percentage,
            MinimumFee = request.MinimumFee,
            MaximumFee = request.MaximumFee,
            TaxRate = request.TaxRate,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = request.Status.Trim()
        };

        await _dbContext.FeeRules.AddAsync(rule, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        rule.AccountType = await _dbContext.AccountTypes
            .AsNoTracking()
            .FirstAsync(type => type.Id == rule.AccountTypeId, cancellationToken);

        return rule.ToResponse();
    }

    /// Validates the request and updates an existing fee rule.
    public async Task<FeeRuleResponse> UpdateFeeRuleAsync(
        long id,
        UpdateFeeRuleRequest request,
        CancellationToken cancellationToken)
    {
        var rule = await _dbContext.FeeRules
            .FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);

        if (rule is null)
        {
            throw new NotFoundException(MessageCode.FeeRuleNotFound);
        }

        await ValidateAsync(request.AccountTypeId, request.FeeType, request.Amount, request.Percentage,
            request.MinimumFee, request.MaximumFee, request.TaxRate, request.EffectiveFrom, request.EffectiveTo,
            request.Status, cancellationToken);

        rule.AccountTypeId = request.AccountTypeId;
        rule.FeeType = request.FeeType;
        rule.Amount = request.Amount;
        rule.Percentage = request.Percentage;
        rule.MinimumFee = request.MinimumFee;
        rule.MaximumFee = request.MaximumFee;
        rule.TaxRate = request.TaxRate;
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
        FeeType feeType,
        decimal? amount,
        decimal? percentage,
        decimal? minimumFee,
        decimal? maximumFee,
        decimal? taxRate,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        string status,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        }

        if (!Enum.IsDefined(feeType))
        {
            throw new ValidationException(MessageCode.InvalidRequest);
        }

        if (amount < 0 || percentage < 0 || minimumFee < 0 || maximumFee < 0 || taxRate < 0)
        {
            throw new ValidationException(MessageCode.InvalidAmount);
        }

        if (minimumFee is not null && maximumFee is not null && minimumFee > maximumFee)
        {
            throw new ValidationException(MessageCode.FeeRuleAmountRangeInvalid);
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
