namespace bams.server.DTO.Jobs;

public sealed record FailedScheduledJobResponse(
    long ExecutionId,
    string JobKey,
    string DisplayName,
    DateTime ScheduledForUtc,
    string TimeZoneId,
    int AttemptNumber,
    DateTime FailedAtUtc,
    int? FailureCode,
    string FailureSummary,
    DateOnly? BusinessDateToClose);
