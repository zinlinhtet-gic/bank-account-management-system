using bams.server.DTO.Common;
using bams.server.DTO.Configuration;

namespace bams.server.Services.Interfaces;

public interface IFeeRateService
{
    /// Gets one page of fee rules, 10 per page.
    Task<PagedResponse<FeeRuleResponse>> GetFeeRulesAsync(
        int page,
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
