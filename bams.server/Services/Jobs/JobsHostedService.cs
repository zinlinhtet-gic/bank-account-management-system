using bams.server.Configuration;
using Microsoft.Extensions.Options;

namespace bams.server.Services.Jobs;

/// <summary>Polls persisted schedules and delegates due work to the reusable jobs operation service.</summary>
public sealed class JobsHostedService : BackgroundService
{
    private const int MinimumPollIntervalSeconds = 1;
    private const int MaximumPollIntervalSeconds = 3600;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobsHostedService> _logger;
    private readonly JobsOptions _options;

    public JobsHostedService(IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        ILogger<JobsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
        if (_options.PollIntervalSeconds is < MinimumPollIntervalSeconds or > MaximumPollIntervalSeconds)
        {
            throw new InvalidOperationException("Jobs:PollIntervalSeconds must be between 1 and 3600.");
        }
    }

    /// <summary>Synchronizes registrations and polls until the application is shutting down.</summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var registrationsSynchronized = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var operations = scope.ServiceProvider.GetRequiredService<IJobsOperationService>();
                if (!registrationsSynchronized)
                {
                    await operations.SynchronizeRegisteredJobsAsync(stoppingToken);
                    registrationsSynchronized = true;
                }

                await operations.RunDueJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "The scheduled jobs polling cycle failed.");
                registrationsSynchronized = false;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
