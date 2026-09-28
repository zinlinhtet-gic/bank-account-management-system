namespace bams.server.Models.Jobs;

/// <summary>Represents the current or most recent state of a scheduled job.</summary>
public enum ScheduledJobStatus
{
    Pending,
    Running,
    RetryScheduled,
    Succeeded,
    Failed,
    Disabled
}
