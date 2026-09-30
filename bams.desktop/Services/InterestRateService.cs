using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;

public sealed class InterestRateService : IInterestRateService
{
    private readonly ApiClient _apiClient;

    public InterestRateService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<PagedResponse<InterestRateResponse>> GetInterestRatesAsync(int page, CancellationToken cancellationToken)
    {
        var query = new QueryString().Add("page", page);

        return _apiClient.GetAsync<PagedResponse<InterestRateResponse>>(ApiConstants.InterestRatesEndpoint + query, cancellationToken);
    }

    public Task<IReadOnlyList<AccountTypeOptionResponse>> GetAccountTypeOptionsAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<AccountTypeOptionResponse>>(ApiConstants.InterestRateAccountTypesEndpoint, cancellationToken);
    }

    public Task<InterestRateResponse> CreateInterestRateAsync(CreateInterestRateRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<CreateInterestRateRequest, InterestRateResponse>(ApiConstants.InterestRatesEndpoint, request, cancellationToken);
    }

    public Task<InterestRateResponse> UpdateInterestRateAsync(long id, UpdateInterestRateRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PutAsync<UpdateInterestRateRequest, InterestRateResponse>($"{ApiConstants.InterestRatesEndpoint}/{id}", request, cancellationToken);
    }
}
