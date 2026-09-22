using bams.server.Models.Accounts.Enums;

namespace bams.server.DTO.Accounts;

public sealed record FixedDepositResponse(
    long Id,
    long AccountId,
    decimal PrincipalAmount,
    long InterestRateRuleId,
    decimal AppliedAnnualRate,
    DateOnly StartDate,
    DateOnly MaturityDate,
    int? TermDays,
    int? TermMonths,
    RenewalInstruction RenewalInstruction,
    long PayoutAccountId,
    FixedDepositStatus Status,
    decimal OriginalPrincipal,
    decimal CurrentPrincipal,
    bool CalculateFromCurrent,
    DateTime CreatedAt,
    DateTime UpdatedAt);
