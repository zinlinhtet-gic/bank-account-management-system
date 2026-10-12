using bams.desktop.Api;
using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

public sealed class EndOfDayClientService(ApiClient apiClient) : IEndOfDayClientService
{
    public Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken) =>
        apiClient.GetAsync<BusinessDateResponse>("/api/operations/business-date", cancellationToken);

    public Task<IReadOnlyList<BusinessDateResponse>> SearchBusinessDatesAsync(DateOnly? fromDate, DateOnly? toDate, CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        var suffix = query.Count > 0 ? "?" + string.Join("&", query) : string.Empty;
        return apiClient.GetAsync<IReadOnlyList<BusinessDateResponse>>($"/api/operations/business-date/dates{suffix}", cancellationToken);
    }

    public Task<EndOfDayRunResponse?> GetLatestRunAsync(DateOnly date, CancellationToken cancellationToken) =>
        apiClient.GetOptionalAsync<EndOfDayRunResponse>($"/api/operations/business-date/dates/{date:yyyy-MM-dd}/latest-run", cancellationToken);

    public Task<EndOfDayRunResponse> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken) =>
        apiClient.PostAsync<EndOfDayRunResponse>($"/api/operations/business-date/{date:yyyy-MM-dd}/pre-close", cancellationToken);

    public Task<EndOfDayRunResponse> ReviewPreCloseAsync(DateOnly date, CancellationToken cancellationToken) =>
        apiClient.PostAsync<EndOfDayRunResponse>($"/api/operations/business-date/{date:yyyy-MM-dd}/review-pre-close", cancellationToken);

    public Task<EndOfDayRunResponse> RequestForceCloseAsync(DateOnly date, ForceCloseRequest request, CancellationToken cancellationToken) =>
        apiClient.PostAsync<ForceCloseRequest, EndOfDayRunResponse>($"/api/operations/business-date/{date:yyyy-MM-dd}/force-close-request", request, cancellationToken);

    public Task<EndOfDayRunResponse> ApproveForceCloseAsync(long runId, CancellationToken cancellationToken) =>
        apiClient.PostAsync<EndOfDayRunResponse>($"/api/operations/business-date/runs/{runId}/force-close-approve", cancellationToken);

    public Task<EndOfDayRunResponse> ApproveAsync(long runId, CancellationToken cancellationToken) =>
        apiClient.PostAsync<EndOfDayRunResponse>($"/api/operations/business-date/runs/{runId}/approve", cancellationToken);

    public Task<EndOfDayRunResponse> CloseAsync(long runId, CancellationToken cancellationToken) =>
        apiClient.PostAsync<EndOfDayRunResponse>($"/api/operations/business-date/runs/{runId}/close", cancellationToken);
}
