using bams.server.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace bams.server.Services.Jobs;

/// <summary>Collects code-registered job functions and their recurrence rules.</summary>
public sealed class ScheduledJobsBuilder
{
    private const int JobKeyMaximumLength = 100;
    private const int DisplayNameMaximumLength = 160;
    private readonly List<ScheduledJobRegistration> _registrations = [];

    /// <summary>Registers an existing service method without requiring a dedicated handler class.</summary>
    public ScheduledJobsBuilder Add<TService>(
        string jobKey,
        JobSchedule schedule,
        Func<TService, CancellationToken, Task> executeAsync)
        where TService : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);
        ValidateLength(jobKey, JobKeyMaximumLength, nameof(jobKey));
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(executeAsync);

        _registrations.Add(new ScheduledJobRegistration(
            jobKey,
            jobKey,
            schedule,
            (serviceProvider, _, cancellationToken) =>
                executeAsync(serviceProvider.GetRequiredService<TService>(), cancellationToken)));
        return this;
    }

    /// <summary>Registers a service method that also needs occurrence metadata.</summary>
    public ScheduledJobsBuilder Add<TService>(
        string jobKey,
        string displayName,
        JobSchedule schedule,
        Func<TService, ScheduledJobExecutionContext, CancellationToken, Task> executeAsync)
        where TService : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ValidateLength(jobKey, JobKeyMaximumLength, nameof(jobKey));
        ValidateLength(displayName, DisplayNameMaximumLength, nameof(displayName));
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(executeAsync);

        _registrations.Add(new ScheduledJobRegistration(
            jobKey,
            displayName,
            schedule,
            (serviceProvider, executionContext, cancellationToken) =>
                executeAsync(serviceProvider.GetRequiredService<TService>(), executionContext, cancellationToken)));
        return this;
    }

    internal IReadOnlyList<ScheduledJobRegistration> Build()
    {
        var duplicateKey = _registrations.GroupBy(registration => registration.JobKey, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateKey is not null)
        {
            throw new InvalidOperationException($"Scheduled job key '{duplicateKey}' is registered more than once.");
        }

        return _registrations.AsReadOnly();
    }

    // Keeps registrations within the corresponding database column limits.
    private static void ValidateLength(string value, int maximumLength, string parameterName)
    {
        if (value.Length > maximumLength)
        {
            throw new ArgumentException($"Value cannot exceed {maximumLength} characters.", parameterName);
        }
    }
}
