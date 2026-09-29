using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;

public sealed class FeeRateService : IFeeRateService
{
    private readonly ApiClient _apiClient;

    public FeeRateService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<FeeRuleResponse>> GetFeeRulesAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<FeeRuleResponse>>(ApiConstants.FeeRatesEndpoint, cancellationToken);
    }

    public Task<IReadOnlyList<AccountTypeOptionResponse>> GetAccountTypeOptionsAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<AccountTypeOptionResponse>>(ApiConstants.FeeRateAccountTypesEndpoint, cancellationToken);
    }

    public Task<FeeRuleResponse> CreateFeeRuleAsync(CreateFeeRuleRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<CreateFeeRuleRequest, FeeRuleResponse>(ApiConstants.FeeRatesEndpoint, request, cancellationToken);
    }

    public Task<FeeRuleResponse> UpdateFeeRuleAsync(long id, UpdateFeeRuleRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PutAsync<UpdateFeeRuleRequest, FeeRuleResponse>($"{ApiConstants.FeeRatesEndpoint}/{id}", request, cancellationToken);
    }
}
