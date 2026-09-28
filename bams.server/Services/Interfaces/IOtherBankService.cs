using bams.server.DTO.Configuration;

namespace bams.server.Services.Interfaces;

public interface IOtherBankService
{
    /// Gets all other banks.
    Task<IReadOnlyList<OtherBankResponse>> GetOtherBanksAsync(
        CancellationToken cancellationToken);
}
