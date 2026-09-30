using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;

public sealed class BankPolicyService : IBankPolicyService
{
    private readonly ApiClient _apiClient;

    public BankPolicyService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<PagedResponse<BankPolicyResponse>> GetBankPoliciesAsync(int page, CancellationToken cancellationToken)
    {
        var query = new QueryString().Add("page", page);

        return _apiClient.GetAsync<PagedResponse<BankPolicyResponse>>(ApiConstants.BankPoliciesEndpoint + query, cancellationToken);
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
