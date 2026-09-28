namespace bams.server.DTO.Products;

/// <summary>
/// Interest-rate rule fields shown when selecting a product's active rate.
/// </summary>
public sealed record InterestRateRuleResponse(
    long Id,
    long AccountTypeId,
    int? TermDays,
    int? TermMonths,
    decimal? BalanceMin,
    decimal? BalanceMax,
    decimal AnnualRate,
    decimal? EarlyWithdrawalRate,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
