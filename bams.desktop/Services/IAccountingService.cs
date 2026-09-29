using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

public interface IAccountingService
{
    Task<IReadOnlyList<GlAccountResponse>> GetGlAccountsAsync(CancellationToken cancellationToken);
    Task<GlAccountResponse> GetGlAccountByIdAsync(long glAccountId, CancellationToken cancellationToken);
}