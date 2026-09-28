using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Services;


/// Other Banks API calls 

public interface IOtherBankService
{
    /// Loads all correspondent banks.
    Task<IReadOnlyList<OtherBankResponse>> GetOtherBanksAsync(CancellationToken cancellationToken);
}
