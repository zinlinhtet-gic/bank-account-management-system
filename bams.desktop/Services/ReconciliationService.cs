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
}
