namespace bams.server.Models.Jobs;

/// <summary>Stores a registered job's recurrence, next run, and current scheduling state.</summary>
public sealed class ScheduledJob
{
    public long Id { get; set; }

    public string JobKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public ScheduledJobScheduleType ScheduleType { get; set; }

    public long? IntervalTicks { get; set; }

    public int? DayOfMonth { get; set; }

    public TimeSpan? LocalTime { get; set; }

    public string TimeZoneId { get; set; } = "UTC";

    public bool IsEnabled { get; set; } = true;

    public DateTime NextRunAtUtc { get; set; }

    public DateTime? PendingScheduledAtUtc { get; set; }

    public string? LeaseToken { get; set; }

    public DateTime? LeaseUntilUtc { get; set; }

    public DateTime? LastRunAtUtc { get; set; }

    public ScheduledJobStatus Status { get; set; } = ScheduledJobStatus.Pending;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<ScheduledJobExecution> Executions { get; set; } = new List<ScheduledJobExecution>();
}
