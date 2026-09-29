using bams.server.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace bams.server.Services.Jobs;

/// <summary>Registers generic recurring jobs and the single hosted scheduler.</summary>
public static class ScheduledJobsServiceCollectionExtensions
{
    /// <summary>Registers a set of scheduled service functions with persistent tracking.</summary>
    public static IServiceCollection AddScheduledJobs(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ScheduledJobsBuilder> configureJobs)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(configureJobs);

        var builder = new ScheduledJobsBuilder();
        configureJobs(builder);
        services.AddSingleton<IReadOnlyList<ScheduledJobRegistration>>(builder.Build());
        services.Configure<JobsOptions>(configuration.GetSection(JobsOptions.SectionName));
        services.AddScoped<IJobsOperationService, JobsOperationService>();
        services.AddHostedService<JobsHostedService>();
        return services;
    }
}
