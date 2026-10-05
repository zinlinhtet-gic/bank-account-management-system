using bams.desktop.DTOs.Accounting;
using bams.desktop.DTOs.Common;

namespace bams.desktop.Services;

public interface IReconciliationService
{
    Task<AccountReconciliationRunResponse> ReconcileAccountsAsync(AccountReconciliationRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<ReconciliationExceptionResponse>> GetExceptionsAsync(DateOnly? fromDate, DateOnly? toDate, string? status, CancellationToken cancellationToken);
    Task<ReconciliationExceptionResponse> UpdateExceptionAsync(long id, UpdateReconciliationExceptionRequest request, CancellationToken cancellationToken);
    Task<ReconciliationExceptionDetailResponse> GetExceptionByIdAsync(long id, CancellationToken cancellationToken);
}
