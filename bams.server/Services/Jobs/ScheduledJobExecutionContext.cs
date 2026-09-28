namespace bams.server.Services.Jobs;

/// <summary>Identifies the occurrence and retry attempt currently being invoked.</summary>
public sealed record ScheduledJobExecutionContext(
    long JobId,
    long ExecutionId,
    DateTime ScheduledForUtc,
    int AttemptNumber);
