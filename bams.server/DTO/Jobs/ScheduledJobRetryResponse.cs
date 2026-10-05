namespace bams.server.DTO.Jobs;

public sealed record ScheduledJobRetryResponse(
    long RetryRequestId,
    long FailedExecutionId,
    DateTime ScheduledForUtc,
    string Status,
    DateTime RequestedAtUtc);
