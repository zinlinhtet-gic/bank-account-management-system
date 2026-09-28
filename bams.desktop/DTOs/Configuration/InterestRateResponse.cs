namespace bams.desktop.DTOs.Configuration;

public sealed record InterestRateResponse(
    long Id,
    long AccountTypeId,
    string AccountTypeCode,
    int? TermDays,
    int? TermMonths,
    decimal? BalanceMin,
    decimal? BalanceMax,
    decimal AnnualRate,
    decimal? EarlyWithdrawalRate,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Status);
