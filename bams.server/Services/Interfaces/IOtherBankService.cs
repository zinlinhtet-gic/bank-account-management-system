using bams.server.DTO.Common;
using bams.server.DTO.Configuration;

namespace bams.server.Services.Interfaces;

public interface IOtherBankService
{
    /// Gets one page of other banks, 10 per page.
    Task<PagedResponse<OtherBankResponse>> GetOtherBanksAsync(
        int page,
        CancellationToken cancellationToken);
}
