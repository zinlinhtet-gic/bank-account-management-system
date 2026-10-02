using bams.desktop.Api;
using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

public sealed class CashOperationsClientService(ApiClient apiClient) : ICashOperationsClientService
{
    public Task<IReadOnlyList<CashPositionSessionResponse>> GetSessionsAsync(DateOnly? date, long? branchId, CancellationToken cancellationToken)
    {
        var endpoint = "/api/cash-operations/sessions";
        if (date.HasValue) endpoint += $"?businessDate={date:yyyy-MM-dd}";
        if (branchId.HasValue) endpoint += $"{(date.HasValue ? "&" : "?")}branchId={branchId.Value}";
        return apiClient.GetAsync<IReadOnlyList<CashPositionSessionResponse>>(endpoint, cancellationToken);
    }

    public Task<CashPositionSessionDetailResponse> GetSessionDetailAsync(long sessionId, CancellationToken cancellationToken) =>
        apiClient.GetAsync<CashPositionSessionDetailResponse>($"/api/cash-operations/sessions/{sessionId}", cancellationToken);

    public Task<CashPositionSessionResponse> OpenSessionAsync(OpenCashSessionRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<OpenCashSessionRequest, CashPositionSessionResponse>("/api/cash-operations/sessions", request, cancellationToken);

    public Task<CashPositionSessionResponse> TransferCashAsync(long sessionId, TransferCashRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<TransferCashRequest, CashPositionSessionResponse>($"/api/cash-operations/sessions/{sessionId}/transfers", request, cancellationToken);

    public Task<CashCountResponse> SubmitCountAsync(long sessionId, SubmitCashCountRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<SubmitCashCountRequest, CashCountResponse>($"/api/cash-operations/sessions/{sessionId}/count", request, cancellationToken);

    public Task<IReadOnlyList<CashAdjustmentResponse>> GetAdjustmentsAsync(DateOnly? date, long? branchId, string? status, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (date.HasValue) query.Add($"businessDate={date.Value:yyyy-MM-dd}");
        if (branchId.HasValue) query.Add($"branchId={branchId.Value}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
        return apiClient.GetAsync<IReadOnlyList<CashAdjustmentResponse>>("/api/cash-operations/adjustments" + suffix, cancellationToken);
    }

    public Task<CashAdjustmentResponse> RequestAdjustmentAsync(long sessionId, RequestCashAdjustmentRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<RequestCashAdjustmentRequest, CashAdjustmentResponse>($"/api/cash-operations/sessions/{sessionId}/adjustments", request, cancellationToken);

    public Task<CashAdjustmentResponse> ApproveAdjustmentAsync(long adjustmentId, CancellationToken cancellationToken) =>
        apiClient.PostAsync<CashAdjustmentResponse>($"/api/cash-operations/adjustments/{adjustmentId}/approve", cancellationToken);
}
