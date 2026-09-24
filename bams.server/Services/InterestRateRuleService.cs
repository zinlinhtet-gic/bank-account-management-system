using bams.server.Data;
using bams.server.DTO.Products;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class InterestRateRuleService : IInterestRateRuleService
{
    private const string ActiveStatus = "Active";

    private readonly ApplicationDbContext _dbContext;

    public InterestRateRuleService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<InterestRateRuleResponse>> GetAvailableRulesAsync(
        long accountTypeId,
        CancellationToken cancellationToken)
    {
        if (accountTypeId <= 0)
        {
            throw new ValidationException(MessageCode.InvalidRequest);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var accountTypeExists = await _dbContext.AccountTypes
            .AsNoTracking()
            .AnyAsync(accountType => accountType.Id == accountTypeId, cancellationToken);

        if (!accountTypeExists)
        {
            throw new NotFoundException(MessageCode.AccountTypeNotFound);
        }

        return await _dbContext.InterestRateRules
            .AsNoTracking()
            .Where(rule =>
                rule.AccountTypeId == accountTypeId &&
                rule.Status == ActiveStatus &&
                rule.EffectiveFrom <= today &&
                (!rule.EffectiveTo.HasValue || rule.EffectiveTo.Value >= today))
            .OrderBy(rule => rule.TermMonths ?? int.MaxValue)
            .ThenBy(rule => rule.TermDays ?? int.MaxValue)
            .ThenBy(rule => rule.BalanceMin)
            .Select(rule => new InterestRateRuleResponse(
                rule.Id,
                rule.AccountTypeId,
                rule.TermDays,
                rule.TermMonths,
                rule.BalanceMin,
                rule.BalanceMax,
                rule.AnnualRate,
                rule.EarlyWithdrawalRate,
                rule.EffectiveFrom,
                rule.EffectiveTo))
            .ToListAsync(cancellationToken);
    }
}
