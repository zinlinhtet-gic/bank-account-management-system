using bams.server.Configuration;

namespace bams.server.Services.Jobs;

/// <summary>Connects a schedule definition to an existing scoped service function.</summary>
public sealed record ScheduledJobRegistration(
    string JobKey,
    string DisplayName,
    JobSchedule Schedule,
    Func<IServiceProvider, ScheduledJobExecutionContext, CancellationToken, Task> ExecuteAsync);
