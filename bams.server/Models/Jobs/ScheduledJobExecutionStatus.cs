namespace bams.server.Models.Jobs;

/// <summary>Represents the outcome of one attempt to execute a scheduled occurrence.</summary>
public enum ScheduledJobExecutionStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled
}
