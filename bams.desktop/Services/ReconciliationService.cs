using bams.desktop.Api;
using bams.desktop.DTOs.Accounting;
using bams.desktop.DTOs.Common;

namespace bams.desktop.Services;

public sealed class ReconciliationService(ApiClient apiClient) : IReconciliationService
{
    public Task<AccountReconciliationRunResponse> ReconcileAccountsAsync(AccountReconciliationRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<AccountReconciliationRequest, AccountReconciliationRunResponse>("/api/accounting/reconciliation/accounts", request, cancellationToken);

    public Task<PagedResponse<ReconciliationExceptionResponse>> GetExceptionsAsync(DateOnly? fromDate, DateOnly? toDate, string? status, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
        return apiClient.GetAsync<PagedResponse<ReconciliationExceptionResponse>>("/api/accounting/reconciliation/exceptions" + suffix, cancellationToken);
    }

    public Task<ReconciliationExceptionResponse> UpdateExceptionAsync(long id, UpdateReconciliationExceptionRequest request, CancellationToken cancellationToken) =>
        apiClient.PatchAsync<UpdateReconciliationExceptionRequest, ReconciliationExceptionResponse>($"/api/accounting/reconciliation/exceptions/{id}", request, cancellationToken);

    public Task<ReconciliationExceptionDetailResponse> GetExceptionByIdAsync(long id, CancellationToken cancellationToken) =>
        apiClient.GetAsync<ReconciliationExceptionDetailResponse>($"/api/accounting/reconciliation/exceptions/{id}", cancellationToken);

    public Task<ReconciliationExceptionResponse> RequestTransactionCorrectionAsync(long id, RequestTransactionCorrectionRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<RequestTransactionCorrectionRequest, ReconciliationExceptionResponse>($"/api/accounting/reconciliation/exceptions/{id}/correction-requests", request, cancellationToken);

    public Task<ReconciliationExceptionResponse> ReviewTransactionCorrectionAsync(long id, ReviewTransactionCorrectionRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<ReviewTransactionCorrectionRequest, ReconciliationExceptionResponse>($"/api/accounting/reconciliation/exceptions/{id}/correction-review", request, cancellationToken);

    public Task<IReadOnlyList<CorrectionTransactionCandidateResponse>> GetCorrectionCandidatesAsync(long id, CancellationToken cancellationToken) =>
        apiClient.GetAsync<IReadOnlyList<CorrectionTransactionCandidateResponse>>($"/api/accounting/reconciliation/exceptions/{id}/correction-candidates", cancellationToken);

    public Task<IReadOnlyList<ReconciliationAccountOptionResponse>> SearchAccountsAsync(string? search, CancellationToken cancellationToken)
    {
        var suffix = string.IsNullOrWhiteSpace(search) ? string.Empty : $"?search={Uri.EscapeDataString(search.Trim())}";
        return apiClient.GetAsync<IReadOnlyList<ReconciliationAccountOptionResponse>>($"/api/accounting/reconciliation/account-options{suffix}", cancellationToken);
    }

    public Task<IReadOnlyList<ReconciliationStaffOptionResponse>> GetInvestigatorOptionsAsync(CancellationToken cancellationToken) =>
        apiClient.GetAsync<IReadOnlyList<ReconciliationStaffOptionResponse>>("/api/accounting/reconciliation/staff-options", cancellationToken);
}
