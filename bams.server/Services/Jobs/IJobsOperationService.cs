namespace bams.server.Services.Jobs;

using bams.server.DTO.Jobs;

/// <summary>Synchronizes registered schedules and runs due occurrences.</summary>
public interface IJobsOperationService
{
    /// <summary>Synchronizes code-registered definitions into the persistent jobs table.</summary>
    Task SynchronizeRegisteredJobsAsync(CancellationToken cancellationToken);

    /// <summary>Claims and invokes due registered jobs.</summary>
    Task RunDueJobsAsync(CancellationToken cancellationToken);

    /// <summary>Extends the active lease when the caller still owns it.</summary>
    Task<bool> RenewLeaseAsync(long jobId, string leaseToken, CancellationToken cancellationToken);

    /// <summary>Lists exhausted failure occurrences still awaiting a successful retry.</summary>
    Task<IReadOnlyList<FailedScheduledJobResponse>> GetFinalFailuresAsync(CancellationToken cancellationToken);

    /// <summary>Queues a manager retry of the same scheduled occurrence and retains the attempt history.</summary>
    Task<ScheduledJobRetryResponse> RequestManualRetryAsync(long failedExecutionId, CancellationToken cancellationToken);
}
