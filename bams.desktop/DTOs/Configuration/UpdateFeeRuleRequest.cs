namespace bams.desktop.DTOs.Configuration;

public sealed record UpdateFeeRuleRequest(
    long AccountTypeId,
    FeeType FeeType,
    decimal? Amount,
    decimal? Percentage,
    decimal? MinimumFee,
    decimal? MaximumFee,
    decimal? TaxRate,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Status);
