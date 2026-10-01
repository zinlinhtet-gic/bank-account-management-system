namespace bams.desktop.DTOs.Configuration;

public sealed record FeeRuleResponse(
    long Id,
    long AccountTypeId,
    string AccountTypeCode,
    FeeType FeeType,
    decimal? Amount,
    decimal? Percentage,
    decimal? MinimumFee,
    decimal? MaximumFee,
    decimal? TaxRate,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Status);
