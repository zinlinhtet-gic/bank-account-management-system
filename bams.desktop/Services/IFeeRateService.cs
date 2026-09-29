using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;

/// Fee Rate API calls
public interface IFeeRateService
{
    /// Loads all fee rules.
    Task<IReadOnlyList<FeeRuleResponse>> GetFeeRulesAsync(CancellationToken cancellationToken);

    /// Loads the account types selectable in the form.
    Task<IReadOnlyList<AccountTypeOptionResponse>> GetAccountTypeOptionsAsync(CancellationToken cancellationToken);

    /// Creates a new fee rule and returns the saved record.
    Task<FeeRuleResponse> CreateFeeRuleAsync(CreateFeeRuleRequest request, CancellationToken cancellationToken);

    /// Updates a fee rule and returns the saved record.
    Task<FeeRuleResponse> UpdateFeeRuleAsync(long id, UpdateFeeRuleRequest request, CancellationToken cancellationToken);
}
