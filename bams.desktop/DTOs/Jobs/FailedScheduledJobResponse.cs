namespace bams.desktop.DTOs.Jobs;

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
    DateOnly? BusinessDateToClose)
{
    public bool RequiresBusinessDateClose => BusinessDateToClose.HasValue;
}
