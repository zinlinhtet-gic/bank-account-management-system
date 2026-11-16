using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Customers;

namespace bams.desktop.Services;

/// <summary>NRC reference-data API calls. Transport, token and error translation are handled by <see cref="ApiClient"/>.</summary>
public sealed class NrcLookupClientService : INrcLookupClientService
{
    private readonly ApiClient _apiClient;

    public NrcLookupClientService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<NrcStateResponse>> GetStatesAsync(CancellationToken cancellationToken) =>
        _apiClient.GetAsync<IReadOnlyList<NrcStateResponse>>(ApiConstants.NrcStatesEndpoint, cancellationToken);

    public Task<IReadOnlyList<NrcTownshipResponse>> GetTownshipsAsync(string stateCode, CancellationToken cancellationToken) =>
        _apiClient.GetAsync<IReadOnlyList<NrcTownshipResponse>>(
            $"{ApiConstants.NrcStatesEndpoint}/{Uri.EscapeDataString(stateCode)}/townships",
            cancellationToken);
}
