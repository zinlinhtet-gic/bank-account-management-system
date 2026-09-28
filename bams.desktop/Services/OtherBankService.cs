using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;

public sealed class OtherBankService : IOtherBankService
{
    private readonly ApiClient _apiClient;

    public OtherBankService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<OtherBankResponse>> GetOtherBanksAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<OtherBankResponse>>(ApiConstants.OtherBanksEndpoint, cancellationToken);
    }
}
