namespace bams.server.Configuration;

/// <summary>Per-client-IP request budgets for a single API server process.</summary>
public sealed class ApiRateLimitOptions
{
    public const string SectionName = "RateLimiting";
    public const string LoginPolicy = "login";
    public int ApiRequestsPerMinute { get; set; } = 120;
    public int LoginRequestsPerMinute { get; set; } = 20;
}
