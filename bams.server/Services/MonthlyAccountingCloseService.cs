using bams.server.Services.Jobs;
using bams.server.Utils;

namespace bams.server.Services;

/// <summary>Runs prior-month maintenance and interest postings before publishing the monthly GL summary.</summary>
public sealed class MonthlyAccountingCloseService(
    AccountMaintenanceService maintenance,
    InterestAccumulationService interest,
    MonthlySummaryGenerationService summaries)
{
    public Task ValidateRetryAsync(DateTime scheduledForUtc, CancellationToken cancellationToken) =>
        summaries.EnsurePreviousMonthClosedAsync(scheduledForUtc, cancellationToken);

    public async Task ExecuteAsync(ScheduledJobExecutionContext execution, CancellationToken cancellationToken)
    {
        await summaries.EnsurePreviousMonthClosedAsync(execution.ScheduledForUtc, cancellationToken);
        await maintenance.ExecuteAsync(execution, cancellationToken);
        await interest.ExecuteAsync(execution, cancellationToken);
        await summaries.GeneratePreviousMonthAsync(BusinessTime.ToBusinessDate(execution.ScheduledForUtc), cancellationToken);
    }
}
