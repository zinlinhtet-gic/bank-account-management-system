using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;

/// <summary>
/// Bank Policies API calls (<c>api/bank-policies</c>).
/// </summary>
public interface IBankPolicyService
{
    /// <summary>
    /// Loads one page of bank policies, 10 per page.
    /// </summary>
    Task<PagedResponse<BankPolicyResponse>> GetBankPoliciesAsync(int page, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new bank policy and returns the saved record.
    /// </summary>
    Task<BankPolicyResponse> CreateBankPolicyAsync(CreateBankPolicyRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a bank policy and returns the saved record.
    /// </summary>
    Task<BankPolicyResponse> UpdateBankPolicyAsync(long id, UpdateBankPolicyRequest request, CancellationToken cancellationToken);
}
