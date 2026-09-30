using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;

public sealed class BankPolicyService : IBankPolicyService
{
    private readonly ApiClient _apiClient;

    public BankPolicyService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<BankPolicyResponse>> GetBankPoliciesAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<BankPolicyResponse>>(ApiConstants.BankPoliciesEndpoint, cancellationToken);
    }

    public Task<BankPolicyResponse> CreateBankPolicyAsync(CreateBankPolicyRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<CreateBankPolicyRequest, BankPolicyResponse>(ApiConstants.BankPoliciesEndpoint, request, cancellationToken);
    }

    public Task<BankPolicyResponse> UpdateBankPolicyAsync(long id, UpdateBankPolicyRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PutAsync<UpdateBankPolicyRequest, BankPolicyResponse>($"{ApiConstants.BankPoliciesEndpoint}/{id}", request, cancellationToken);
    }
}
