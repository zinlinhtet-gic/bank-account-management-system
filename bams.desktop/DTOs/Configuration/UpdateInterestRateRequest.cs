namespace bams.desktop.DTOs.Configuration;

public sealed record UpdateInterestRateRequest(
    long AccountTypeId,
    int? TermDays,
    int? TermMonths,
    decimal? BalanceMin,
    decimal? BalanceMax,
    decimal AnnualRate,
    decimal? EarlyWithdrawalRate,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Status);
