using bams.desktop.Api;
using bams.desktop.DTOs.Jobs;

namespace bams.desktop.Services;

public sealed class ScheduledJobClientService(ApiClient apiClient) : IScheduledJobClientService
{
    public Task<IReadOnlyList<FailedScheduledJobResponse>> GetFailuresAsync(CancellationToken cancellationToken) =>
        apiClient.GetAsync<IReadOnlyList<FailedScheduledJobResponse>>(
            "/api/operations/scheduled-jobs/failures", cancellationToken);

    public Task<ScheduledJobRetryResponse> RetryAsync(long executionId, CancellationToken cancellationToken) =>
        apiClient.PostAsync<ScheduledJobRetryResponse>(
            $"/api/operations/scheduled-jobs/executions/{executionId}/retry", cancellationToken);
}
