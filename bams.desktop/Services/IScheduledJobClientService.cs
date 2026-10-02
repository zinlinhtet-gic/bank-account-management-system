using bams.desktop.DTOs.Jobs;

namespace bams.desktop.Services;

public interface IScheduledJobClientService
{
    Task<IReadOnlyList<FailedScheduledJobResponse>> GetFailuresAsync(CancellationToken cancellationToken);
    Task<ScheduledJobRetryResponse> RetryAsync(long executionId, CancellationToken cancellationToken);
}
