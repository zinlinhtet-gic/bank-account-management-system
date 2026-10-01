using bams.server.DTO.Configuration;
using bams.server.Models.Products;

namespace bams.server.Mapping;

public static class FeeRuleMappings
{
    // Converts a FeeRule entity (with AccountType loaded) into the API response contract.
    public static FeeRuleResponse ToResponse(this FeeRule rule)
    {
        return new FeeRuleResponse(
            rule.Id,
            rule.AccountTypeId,
            rule.AccountType?.Code ?? string.Empty,
            rule.FeeType,
            rule.Amount,
            rule.Percentage,
            rule.MinimumFee,
            rule.MaximumFee,
            rule.TaxRate,
            rule.EffectiveFrom,
            rule.EffectiveTo,
            rule.Status);
    }
}
