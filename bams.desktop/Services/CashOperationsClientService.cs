using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

public sealed class CashOperationsClientService(ApiClient apiClient) : ICashOperationsClientService
{
    public Task<IReadOnlyList<CashPositionSessionResponse>> GetSessionsAsync(DateOnly? date, CancellationToken cancellationToken)
    {
        var endpoint = "/api/cash-operations/sessions";
        if (date.HasValue) endpoint += $"?businessDate={date:yyyy-MM-dd}";
        return apiClient.GetAsync<IReadOnlyList<CashPositionSessionResponse>>(endpoint, cancellationToken);
    }

    public Task<CashPositionSessionDetailResponse> GetSessionDetailAsync(long sessionId, CancellationToken cancellationToken) =>
        apiClient.GetAsync<CashPositionSessionDetailResponse>($"/api/cash-operations/sessions/{sessionId}", cancellationToken);

    public Task<CashPositionSessionResponse> OpenSessionAsync(OpenCashSessionRequest request, string idempotencyKey, CancellationToken cancellationToken) =>
        apiClient.PostAsync<OpenCashSessionRequest, CashPositionSessionResponse>("/api/cash-operations/sessions", request,
            new Dictionary<string, string> { [ApiConstants.IdempotencyKeyHeader] = idempotencyKey }, cancellationToken);

    public Task<CashPositionSessionResponse> TransferCashAsync(long sessionId, TransferCashRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<TransferCashRequest, CashPositionSessionResponse>($"/api/cash-operations/sessions/{sessionId}/transfers", request, cancellationToken);

    public Task<CashCountResponse> SubmitCountAsync(long sessionId, SubmitCashCountRequest request, string idempotencyKey, CancellationToken cancellationToken) =>
        apiClient.PostAsync<SubmitCashCountRequest, CashCountResponse>($"/api/cash-operations/sessions/{sessionId}/count", request,
            new Dictionary<string, string> { [ApiConstants.IdempotencyKeyHeader] = idempotencyKey }, cancellationToken);

    public Task<IReadOnlyList<CashHandoffRecipientResponse>> GetHandoffRecipientsAsync(CancellationToken cancellationToken) =>
        apiClient.GetAsync<IReadOnlyList<CashHandoffRecipientResponse>>("/api/cash-handoffs/recipients", cancellationToken);

    public Task<IReadOnlyList<CashHandoffResponse>> GetCashHandoffsAsync(DateOnly? date, CancellationToken cancellationToken)
    {
        var suffix = date.HasValue ? $"?businessDate={date.Value:yyyy-MM-dd}" : string.Empty;
        return apiClient.GetAsync<IReadOnlyList<CashHandoffResponse>>("/api/cash-handoffs" + suffix, cancellationToken);
    }

    public Task<CashHandoffDetailResponse> GetCashHandoffDetailAsync(long handoffId, CancellationToken cancellationToken) =>
        apiClient.GetAsync<CashHandoffDetailResponse>($"/api/cash-handoffs/{handoffId}", cancellationToken);

    public Task<CashHandoffResponse> AcceptCashHandoffAsync(long handoffId, CashHandoffActionRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<CashHandoffActionRequest, CashHandoffResponse>($"/api/cash-handoffs/{handoffId}/accept", request, cancellationToken);

    public Task<CashHandoffResponse> DeclineCashHandoffAsync(long handoffId, CashHandoffActionRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<CashHandoffActionRequest, CashHandoffResponse>($"/api/cash-handoffs/{handoffId}/decline", request, cancellationToken);

    public Task<CashHandoffResponse> ReassignCashHandoffAsync(long handoffId, ReassignCashHandoffRequest request, CancellationToken cancellationToken) =>
        apiClient.PutAsync<ReassignCashHandoffRequest, CashHandoffResponse>($"/api/cash-handoffs/{handoffId}/recipient", request, cancellationToken);

    public Task<IReadOnlyList<CashAdjustmentResponse>> GetAdjustmentsAsync(DateOnly? date, string? status, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (date.HasValue) query.Add($"businessDate={date.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        var suffix = query.Count == 0 ? string.Empty : "?" + string.Join("&", query);
        return apiClient.GetAsync<IReadOnlyList<CashAdjustmentResponse>>("/api/cash-operations/adjustments" + suffix, cancellationToken);
    }

    public Task<CashAdjustmentResponse> RequestAdjustmentAsync(long sessionId, RequestCashAdjustmentRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<RequestCashAdjustmentRequest, CashAdjustmentResponse>($"/api/cash-operations/sessions/{sessionId}/adjustments", request, cancellationToken);

    public Task<CashAdjustmentResponse> ApproveAdjustmentAsync(long adjustmentId, CancellationToken cancellationToken) =>
        apiClient.PostAsync<CashAdjustmentResponse>($"/api/cash-operations/adjustments/{adjustmentId}/approve", cancellationToken);
}
