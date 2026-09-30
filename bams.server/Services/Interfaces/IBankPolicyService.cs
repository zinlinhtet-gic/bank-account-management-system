using bams.server.DTO.Configuration;

namespace bams.server.Services.Interfaces;

public interface IBankPolicyService
{
    /// <summary>
    /// Gets all bank policies (account types).
    /// </summary>
    Task<IReadOnlyList<BankPolicyResponse>> GetBankPoliciesAsync(
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
