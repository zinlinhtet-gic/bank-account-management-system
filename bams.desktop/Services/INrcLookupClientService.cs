using bams.desktop.DTOs.Customers;

namespace bams.desktop.Services;

/// <summary>NRC state/township reference-data lookups, used by the Customer Create form's NRC-number builder.</summary>
public interface INrcLookupClientService
{
    Task<IReadOnlyList<NrcStateResponse>> GetStatesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<NrcTownshipResponse>> GetTownshipsAsync(string stateCode, CancellationToken cancellationToken);
}
