using bams.server.DTO.Configuration;

namespace bams.server.Services.Interfaces;

public interface IFeeRateService
{
    /// Gets all fee rules.
    Task<IReadOnlyList<FeeRuleResponse>> GetFeeRulesAsync(
        CancellationToken cancellationToken);

    /// Gets the account types selectable in the fee rule form.
    Task<IReadOnlyList<AccountTypeOptionResponse>> GetAccountTypeOptionsAsync(
        CancellationToken cancellationToken);

    /// Creates a new fee rule.
    Task<FeeRuleResponse> CreateFeeRuleAsync(
        CreateFeeRuleRequest request,
        CancellationToken cancellationToken);

    /// Updates an existing fee rule.
    Task<FeeRuleResponse> UpdateFeeRuleAsync(
        long id,
        UpdateFeeRuleRequest request,
        CancellationToken cancellationToken);
}
