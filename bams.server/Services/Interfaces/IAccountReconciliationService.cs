using bams.server.DTO.Accounting;
using bams.server.DTO.Common;

namespace bams.server.Services.Interfaces;

public interface IAccountReconciliationService
{
    Task<AccountReconciliationRunResponse> ReconcileAccountsAsync(AccountReconciliationRequest request, CancellationToken cancellationToken);
    Task<AccountReconciliationRunResponse> ReconcileAccountsAsAsync(AccountReconciliationRequest request, long actorId, long? scheduledJobExecutionId, CancellationToken cancellationToken);
    Task<PagedResponse<ReconciliationExceptionResponse>> GetExceptionsAsync(DateOnly? fromDate, DateOnly? toDate, string? status, int? page, int? pageSize, CancellationToken cancellationToken);
    Task<ReconciliationExceptionDetailResponse> GetExceptionByIdAsync(long exceptionId, CancellationToken cancellationToken);
    Task<ReconciliationExceptionResponse> UpdateExceptionAsync(long exceptionId, UpdateReconciliationExceptionRequest request, CancellationToken cancellationToken);
    Task<ReconciliationExceptionResponse> RequestTransactionCorrectionAsync(long exceptionId, RequestTransactionCorrectionRequest request, CancellationToken cancellationToken);
    Task<ReconciliationExceptionResponse> ReviewTransactionCorrectionAsync(long exceptionId, ReviewTransactionCorrectionRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CorrectionTransactionCandidateResponse>> GetCorrectionCandidatesAsync(long exceptionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReconciliationAccountOptionResponse>> SearchAccountsAsync(string? search, CancellationToken cancellationToken);
    Task<IReadOnlyList<ReconciliationStaffOptionResponse>> GetInvestigatorOptionsAsync(CancellationToken cancellationToken);
}
