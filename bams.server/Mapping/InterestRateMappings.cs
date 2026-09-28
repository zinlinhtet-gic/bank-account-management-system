using bams.server.DTO.Configuration;
using bams.server.Models.Products;

namespace bams.server.Mapping;

public static class InterestRateMappings
{
    // Converts an InterestRateRule entity (with AccountType loaded) into the API response contract.
    public static InterestRateResponse ToResponse(this InterestRateRule rule)
    {
        return new InterestRateResponse(
            rule.Id,
            rule.AccountTypeId,
            rule.AccountType?.Code ?? string.Empty,
            rule.TermDays,
            rule.TermMonths,
            rule.BalanceMin,
            rule.BalanceMax,
            rule.AnnualRate,
            rule.EarlyWithdrawalRate,
            rule.EffectiveFrom,
            rule.EffectiveTo,
            rule.Status);
    }
}
