using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;


/// Other Banks API calls

public interface IOtherBankService
{
    /// Loads one page of correspondent banks, 10 per page.
    Task<PagedResponse<OtherBankResponse>> GetOtherBanksAsync(int page, CancellationToken cancellationToken);
}
