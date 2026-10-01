namespace bams.server.DTO.Configuration;

public sealed record CreateInterestRateRequest(
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
