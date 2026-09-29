using bams.server.Configuration;
using bams.server.Data;
using bams.server.Models.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace bams.server.Services.Jobs;

/// <summary>Persists job definitions and coordinates leased, retryable executions.</summary>
public sealed class JobsOperationService : IJobsOperationService
{
    private const int ErrorMessageMaximumLength = 4000;
    private const int MinimumHeartbeatIntervalSeconds = 1;
    private readonly ApplicationDbContext _dbContext;
    private readonly IReadOnlyDictionary<string, ScheduledJobRegistration> _registrations;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly JobsOptions _options;
    private readonly ILogger<JobsOperationService> _logger;

    public JobsOperationService(
        ApplicationDbContext dbContext,
        IReadOnlyList<ScheduledJobRegistration> registrations,
        IServiceScopeFactory scopeFactory,
        IOptions<JobsOptions> options,
        ILogger<JobsOperationService> logger)
    {
        _dbContext = dbContext;
        _registrations = registrations.ToDictionary(registration => registration.JobKey, StringComparer.OrdinalIgnoreCase);
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        ValidateOptions(_options);
    }

    /// <inheritdoc />
    public async Task SynchronizeRegisteredJobsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var jobs = await _dbContext.ScheduledJobs.ToListAsync(cancellationToken);
        foreach (var registration in _registrations.Values)
        {
            var job = jobs.SingleOrDefault(item =>
                string.Equals(item.JobKey, registration.JobKey, StringComparison.OrdinalIgnoreCase));
            if (job is null)
            {
                job = new ScheduledJob
                {
                    JobKey = registration.JobKey,
                    DisplayName = registration.DisplayName,
                    IsEnabled = true,
                    Status = ScheduledJobStatus.Pending,
                    NextRunAtUtc = registration.Schedule.GetNextRunAtUtc(now),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };
                registration.Schedule.ApplyTo(job);
                _dbContext.ScheduledJobs.Add(job);
                continue;
            }

            var scheduleChanged = !registration.Schedule.Matches(job);
            registration.Schedule.ApplyTo(job);
            job.DisplayName = registration.DisplayName;
            job.IsEnabled = true;
            if (scheduleChanged)
            {
                job.NextRunAtUtc = registration.Schedule.GetNextRunAtUtc(now);
                if (!job.LeaseUntilUtc.HasValue || job.LeaseUntilUtc <= now)
                {
                    job.PendingScheduledAtUtc = null;
                    job.LeaseToken = null;
                    job.LeaseUntilUtc = null;
                }
            }
            if (job.Status == ScheduledJobStatus.Disabled)
            {
                job.Status = ScheduledJobStatus.Pending;
            }
            job.UpdatedAtUtc = now;
        }

        foreach (var job in jobs.Where(job => !_registrations.ContainsKey(job.JobKey)))
        {
            job.IsEnabled = false;
            job.Status = ScheduledJobStatus.Disabled;
            job.LeaseToken = null;
            job.LeaseUntilUtc = null;
            job.UpdatedAtUtc = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task RunDueJobsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var dueJobIds = await _dbContext.ScheduledJobs.AsNoTracking()
            .Where(job => job.IsEnabled && job.NextRunAtUtc <= now &&
                (!job.LeaseUntilUtc.HasValue || job.LeaseUntilUtc <= now))
            .OrderBy(job => job.NextRunAtUtc)
            .ThenBy(job => job.Id)
            .Select(job => job.Id)
            .Take(_options.MaximumJobsPerPoll)
            .ToListAsync(cancellationToken);

        foreach (var jobId in dueJobIds)
        {
            await ClaimAndExecuteJobAsync(jobId, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task<bool> RenewLeaseAsync(long jobId, string leaseToken, CancellationToken cancellationToken)
    {
        var newLeaseUntil = DateTime.UtcNow.AddSeconds(_options.LeaseDurationSeconds);
        var updated = await _dbContext.ScheduledJobs
            .Where(job => job.Id == jobId && job.IsEnabled && job.LeaseToken == leaseToken)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.LeaseUntilUtc, newLeaseUntil)
                .SetProperty(job => job.UpdatedAtUtc, DateTime.UtcNow), cancellationToken);
        return updated > 0;
    }

    // Claims one occurrence atomically, then executes it outside the database transaction.
    private async Task ClaimAndExecuteJobAsync(long jobId, CancellationToken stoppingToken)
    {
        var claim = await TryClaimJobAsync(jobId, stoppingToken);
        if (claim is null)
        {
            return;
        }

        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var leaseHeartbeat = KeepLeaseAliveAsync(jobId, claim.LeaseToken, executionCancellation, stoppingToken);
        Exception? failure = null;
        var cancelled = false;
        try
        {
            if (!_registrations.TryGetValue(claim.Job.JobKey, out var registration))
            {
                throw new InvalidOperationException($"No registered function exists for job '{claim.Job.JobKey}'.");
            }

            await using var handlerScope = _scopeFactory.CreateAsyncScope();
            await registration.ExecuteAsync(handlerScope.ServiceProvider, claim.ExecutionContext, executionCancellation.Token);
        }
        catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
        {
            cancelled = true;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            executionCancellation.Cancel();
            try
            {
                await leaseHeartbeat;
            }
            catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
            {
                // Handler completion or host shutdown stops the heartbeat loop.
            }
            catch (Exception exception)
            {
                failure ??= exception;
            }
        }

        await CompleteJobAttemptAsync(claim, failure, cancelled, stoppingToken);
        if (stoppingToken.IsCancellationRequested)
        {
            stoppingToken.ThrowIfCancellationRequested();
        }
    }

    // Acquires a lease and persists the running attempt before invoking user code.
    private async Task<JobExecutionClaim?> TryClaimJobAsync(long jobId, CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var leaseToken = Guid.NewGuid().ToString("D");
        var acquired = await _dbContext.ScheduledJobs
            .Where(job => job.Id == jobId && job.IsEnabled && job.NextRunAtUtc <= now &&
                (!job.LeaseUntilUtc.HasValue || job.LeaseUntilUtc <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.LeaseToken, leaseToken)
                .SetProperty(job => job.LeaseUntilUtc, now.AddSeconds(_options.LeaseDurationSeconds))
                .SetProperty(job => job.PendingScheduledAtUtc,
                    job => job.PendingScheduledAtUtc ?? job.NextRunAtUtc)
                .SetProperty(job => job.Status, ScheduledJobStatus.Running)
                .SetProperty(job => job.UpdatedAtUtc, now), cancellationToken);
        if (acquired == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var job = await _dbContext.ScheduledJobs.AsNoTracking()
            .SingleAsync(item => item.Id == jobId, cancellationToken);
        var scheduledForUtc = job.PendingScheduledAtUtc ?? job.NextRunAtUtc;
        await _dbContext.ScheduledJobExecutions
            .Where(execution => execution.ScheduledJobId == jobId &&
                execution.ScheduledForUtc == scheduledForUtc && execution.Status == ScheduledJobExecutionStatus.Running)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(execution => execution.Status, ScheduledJobExecutionStatus.Failed)
                .SetProperty(execution => execution.CompletedAtUtc, now)
                .SetProperty(execution => execution.ErrorMessage, "The previous worker lease expired before completion."),
                cancellationToken);
        var previousAttempts = await _dbContext.ScheduledJobExecutions.AsNoTracking()
            .Where(execution => execution.ScheduledJobId == jobId && execution.ScheduledForUtc == scheduledForUtc)
            .Select(execution => (int?)execution.AttemptNumber)
            .MaxAsync(cancellationToken) ?? 0;
        if (previousAttempts >= _options.MaximumAttempts)
        {
            var registration = _registrations[job.JobKey];
            await _dbContext.ScheduledJobs.Where(item => item.Id == jobId && item.LeaseToken == leaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, ScheduledJobStatus.Failed)
                    .SetProperty(item => item.LastRunAtUtc, now)
                    .SetProperty(item => item.PendingScheduledAtUtc, (DateTime?)null)
                    .SetProperty(item => item.NextRunAtUtc, registration.Schedule.GetNextRunAtUtc(now))
                    .SetProperty(item => item.LeaseToken, (string?)null)
                    .SetProperty(item => item.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogError("Scheduled job {JobKey} exhausted its attempts after a worker lease expired.", job.JobKey);
            return null;
        }

        var execution = new ScheduledJobExecution
        {
            ScheduledJobId = jobId,
            ScheduledForUtc = scheduledForUtc,
            AttemptNumber = previousAttempts + 1,
            Status = ScheduledJobExecutionStatus.Running,
            StartedAtUtc = now
        };
        _dbContext.ScheduledJobExecutions.Add(execution);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new JobExecutionClaim(job, execution.Id, scheduledForUtc, execution.AttemptNumber, leaseToken);
    }

    // Keeps long-running handlers from losing their cross-instance lease.
    private async Task KeepLeaseAliveAsync(long jobId, string leaseToken,
        CancellationTokenSource executionCancellation, CancellationToken stoppingToken)
    {
        var heartbeatInterval = TimeSpan.FromSeconds(
            Math.Max(MinimumHeartbeatIntervalSeconds, _options.LeaseDurationSeconds / 3));
        while (!stoppingToken.IsCancellationRequested && !executionCancellation.IsCancellationRequested)
        {
            await Task.Delay(heartbeatInterval, executionCancellation.Token);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var operations = scope.ServiceProvider.GetRequiredService<IJobsOperationService>();
            if (!await operations.RenewLeaseAsync(jobId, leaseToken, executionCancellation.Token))
            {
                executionCancellation.Cancel();
                return;
            }
        }
    }

    // Records success, cancellation, or failure and schedules retry or the next recurrence.
    private async Task CompleteJobAttemptAsync(JobExecutionClaim claim, Exception? failure,
        bool cancelled, CancellationToken stoppingToken)
    {
        var cancellationToken = stoppingToken.IsCancellationRequested ? CancellationToken.None : stoppingToken;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var job = await _dbContext.ScheduledJobs.SingleOrDefaultAsync(item => item.Id == claim.Job.Id, cancellationToken);
        if (job is null || job.LeaseToken != claim.LeaseToken)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return;
        }

        var execution = await _dbContext.ScheduledJobExecutions
            .SingleAsync(item => item.Id == claim.ExecutionId, cancellationToken);
        var now = DateTime.UtcNow;
        execution.CompletedAtUtc = now;
        job.LastRunAtUtc = now;
        job.LeaseToken = null;
        job.LeaseUntilUtc = null;
        job.UpdatedAtUtc = now;

        if (failure is null && !cancelled)
        {
            execution.Status = ScheduledJobExecutionStatus.Succeeded;
            job.Status = ScheduledJobStatus.Succeeded;
            job.PendingScheduledAtUtc = null;
            job.NextRunAtUtc = _registrations[claim.Job.JobKey].Schedule.GetNextRunAtUtc(now);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Scheduled job {JobKey} completed successfully.", claim.Job.JobKey);
            return;
        }

        execution.Status = cancelled ? ScheduledJobExecutionStatus.Cancelled : ScheduledJobExecutionStatus.Failed;
        execution.ErrorMessage = FormatFailure(failure, cancelled);
        if (cancelled || claim.AttemptNumber < _options.MaximumAttempts)
        {
            job.Status = ScheduledJobStatus.RetryScheduled;
            job.NextRunAtUtc = now.Add(GetRetryDelay(claim.AttemptNumber));
        }
        else
        {
            job.Status = ScheduledJobStatus.Failed;
            job.PendingScheduledAtUtc = null;
            job.NextRunAtUtc = _registrations[claim.Job.JobKey].Schedule.GetNextRunAtUtc(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        if (failure is not null)
        {
            _logger.LogError(failure, "Scheduled job {JobKey} failed on attempt {AttemptNumber}.",
                claim.Job.JobKey, claim.AttemptNumber);
        }
    }

    // Converts exceptions to bounded text that fits the execution history column.
    private static string FormatFailure(Exception? exception, bool cancelled)
    {
        var message = cancelled ? "Execution was cancelled before completion." :
            exception?.ToString() ?? "Execution was interrupted.";
        return message.Length <= ErrorMessageMaximumLength
            ? message
            : message[..ErrorMessageMaximumLength];
    }

    // Doubles the configured base delay after each failed attempt.
    private TimeSpan GetRetryDelay(int attemptNumber)
    {
        var exponentialMultiplier = 1L << (attemptNumber - 1);
        return TimeSpan.FromSeconds((long)_options.RetryDelaySeconds * exponentialMultiplier);
    }

    // Rejects invalid worker settings before the hosted scheduler starts polling.
    private static void ValidateOptions(JobsOptions options)
    {
        if (options.PollIntervalSeconds is < 1 or > 3600 ||
            options.MaximumJobsPerPoll is < 1 or > 1000 ||
            options.LeaseDurationSeconds is < 3 or > 86_400 ||
            options.MaximumAttempts is < 1 or > 10 ||
            options.RetryDelaySeconds is < 1 or > 86_400)
        {
            throw new InvalidOperationException("One or more Jobs configuration values are outside supported limits.");
        }
    }

    private sealed record JobExecutionClaim(
        ScheduledJob Job,
        long ExecutionId,
        DateTime ScheduledForUtc,
        int AttemptNumber,
        string LeaseToken)
    {
        public ScheduledJobExecutionContext ExecutionContext =>
            new(Job.Id, ExecutionId, ScheduledForUtc, AttemptNumber);
    }
}
