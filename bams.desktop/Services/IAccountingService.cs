using bams.desktop.DTOs.Accounting;
using bams.desktop.DTOs.Common;

namespace bams.desktop.Services;

public interface IAccountingService
{
    Task<IReadOnlyList<GlAccountResponse>> GetGlAccountsAsync(CancellationToken cancellationToken);
    Task<GlAccountResponse> GetGlAccountByIdAsync(long glAccountId, CancellationToken cancellationToken);
    Task<GlAccountDetailResponse> GetGlAccountDetailAsync(long glAccountId, int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResponse<AccountingEntryResponse>> GetAccountingEntriesAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        long? glAccountId,
        EntryType? entryType,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    );
}
