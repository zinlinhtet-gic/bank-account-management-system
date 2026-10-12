namespace bams.server.Models.Jobs;

/// <summary>Audits a manager request to rerun one exhausted scheduled occurrence.</summary>
public sealed class ScheduledJobRetryRequest
{
    public long Id { get; set; }
    public long ScheduledJobId { get; set; }
    public ScheduledJob? ScheduledJob { get; set; }
    public long FailedExecutionId { get; set; }
    public ScheduledJobExecution? FailedExecution { get; set; }
    public DateTime ScheduledForUtc { get; set; }
    public DateTime ResumeNextRunAtUtc { get; set; }
    public long RequestedBy { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public ScheduledJobRetryRequestStatus Status { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
