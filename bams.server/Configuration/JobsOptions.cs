namespace bams.server.Configuration;

/// <summary>Controls how the generic scheduled-job worker polls, leases, and retries jobs.</summary>
public sealed class JobsOptions
{
    public const string SectionName = "Jobs";

    public int PollIntervalSeconds { get; set; } = 15;

    public int MaximumJobsPerPoll { get; set; } = 50;

    public int LeaseDurationSeconds { get; set; } = 120;

    public int MaximumAttempts { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 30;
}
