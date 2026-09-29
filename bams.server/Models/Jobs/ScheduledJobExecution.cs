namespace bams.server.Models.Jobs;

/// <summary>Stores status and error details for one attempt of a scheduled occurrence.</summary>
public sealed class ScheduledJobExecution
{
    public long Id { get; set; }

    public long ScheduledJobId { get; set; }

    public ScheduledJob? ScheduledJob { get; set; }

    public DateTime ScheduledForUtc { get; set; }

    public int AttemptNumber { get; set; }

    public ScheduledJobExecutionStatus Status { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? ErrorMessage { get; set; }
}
