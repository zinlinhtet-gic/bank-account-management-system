using bams.server.DTOs.Customers;

namespace bams.server.Services.Interfaces;

public interface INrcService
{
    IReadOnlyList<NrcStateDto> GetStates();

    IReadOnlyList<NrcTownshipDto> GetTownshipsByState(
        string stateCode);
}