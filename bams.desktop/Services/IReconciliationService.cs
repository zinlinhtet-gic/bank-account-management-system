using bams.desktop.DTOs.Accounting;
using bams.desktop.DTOs.Common;

namespace bams.desktop.Services;

public interface IReconciliationService
{
    Task<AccountReconciliationRunResponse> ReconcileAccountsAsync(AccountReconciliationRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<ReconciliationExceptionResponse>> GetExceptionsAsync(DateOnly? fromDate, DateOnly? toDate, string? status, CancellationToken cancellationToken);
    Task<ReconciliationExceptionResponse> UpdateExceptionAsync(long id, UpdateReconciliationExceptionRequest request, CancellationToken cancellationToken);
    Task<ReconciliationExceptionDetailResponse> GetExceptionByIdAsync(long id, CancellationToken cancellationToken);
    Task<ReconciliationExceptionResponse> RequestTransactionCorrectionAsync(long id, RequestTransactionCorrectionRequest request, CancellationToken cancellationToken);
    Task<ReconciliationExceptionResponse> ReviewTransactionCorrectionAsync(long id, ReviewTransactionCorrectionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CorrectionTransactionCandidateResponse>> GetCorrectionCandidatesAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReconciliationAccountOptionResponse>> SearchAccountsAsync(string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReconciliationStaffOptionResponse>> GetInvestigatorOptionsAsync(CancellationToken cancellationToken);
}
