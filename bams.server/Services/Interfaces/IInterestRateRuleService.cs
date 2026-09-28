using bams.server.DTO.Products;

namespace bams.server.Services.Interfaces;

public interface IInterestRateRuleService
{
    /// <summary>
    /// Lists active interest rules for an account type that are effective on the current UTC date.
    /// </summary>
    Task<IReadOnlyList<InterestRateRuleResponse>> GetAvailableRulesAsync(
        long accountTypeId,
        CancellationToken cancellationToken);
}
