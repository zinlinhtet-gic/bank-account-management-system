using bams.server.DTO.Common;
using bams.server.DTO.Configuration;

namespace bams.server.Services.Interfaces;

public interface IBankPolicyService
{
    /// <summary>
    /// Gets one page of bank policies (account types), 10 per page.
    /// </summary>
    Task<PagedResponse<BankPolicyResponse>> GetBankPoliciesAsync(
        int page,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new bank policy (account type).
    /// </summary>
    Task<BankPolicyResponse> CreateBankPolicyAsync(
        CreateBankPolicyRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an existing bank policy (account type).
    /// </summary>
    Task<BankPolicyResponse> UpdateBankPolicyAsync(
        long id,
        UpdateBankPolicyRequest request,
        CancellationToken cancellationToken);
}
