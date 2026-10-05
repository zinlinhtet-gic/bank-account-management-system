using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;

public sealed class OtherBankService : IOtherBankService
{
    private readonly ApiClient _apiClient;

    public OtherBankService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<PagedResponse<OtherBankResponse>> GetOtherBanksAsync(int page, CancellationToken cancellationToken)
    {
        var query = new QueryString().Add("page", page);

        return _apiClient.GetAsync<PagedResponse<OtherBankResponse>>(ApiConstants.OtherBanksEndpoint + query, cancellationToken);
    }
}
