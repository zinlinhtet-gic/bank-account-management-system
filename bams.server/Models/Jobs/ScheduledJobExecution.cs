namespace bams.server.Models.Jobs;

using bams.server.Messages;

/// <summary>Stores status and error details for one attempt of a scheduled occurrence.</summary>
public sealed class ScheduledJobExecution
{
    public long Id { get; set; }

    public long ScheduledJobId { get; set; }

    public ScheduledJob? ScheduledJob { get; set; }

    public DateTime ScheduledForUtc { get; set; }

    public int AttemptNumber { get; set; }

    public long? RetryRequestId { get; set; }

    public ScheduledJobExecutionStatus Status { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? ErrorMessage { get; set; }

    public MessageCode? FailureCode { get; set; }

    public string? FailureSummary { get; set; }

    public DateTime? FinalFailureAtUtc { get; set; }

    public DateTime? FailureRetryRequestedAtUtc { get; set; }

    public long? FailureRetryRequestedBy { get; set; }

    public DateTime? FailureResolvedAtUtc { get; set; }
}
