namespace bams.desktop.DTOs.Jobs;

public sealed record ScheduledJobRetryResponse(
    long RetryRequestId,
    long FailedExecutionId,
    DateTime ScheduledForUtc,
    string Status,
    DateTime RequestedAtUtc);
