using bams.desktop.Api;
using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

public sealed class EndOfDayClientService(ApiClient apiClient) : IEndOfDayClientService
{
    public Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken) =>
        apiClient.GetAsync<BusinessDateResponse>("/api/operations/business-date", cancellationToken);

    public Task<EndOfDayRunResponse> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken) =>
        apiClient.PostAsync<EndOfDayRunResponse>($"/api/operations/business-date/{date:yyyy-MM-dd}/pre-close", cancellationToken);

    public Task<EndOfDayRunResponse> ApproveAsync(long runId, CancellationToken cancellationToken) =>
        apiClient.PostAsync<EndOfDayRunResponse>($"/api/operations/business-date/runs/{runId}/approve", cancellationToken);

    public Task<EndOfDayRunResponse> CloseAsync(long runId, CancellationToken cancellationToken) =>
        apiClient.PostAsync<EndOfDayRunResponse>($"/api/operations/business-date/runs/{runId}/close", cancellationToken);
}
