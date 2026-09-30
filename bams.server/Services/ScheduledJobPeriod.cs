using bams.server.Utils;
using bams.server.Services.Jobs;

namespace bams.server.Services;

internal static class ScheduledJobPeriod
{
    // Scheduled jobs run on the bank's business clock.
    public const string TimeZoneId = BusinessTime.TimeZoneId;

    public static DateOnly GetRunDate(ScheduledJobExecutionContext context)
    {
        return BusinessTime.ToBusinessDate(context.ScheduledForUtc);
    }

    public static (DateOnly Start, DateOnly End) GetPreviousCalendarMonth(DateOnly runDate)
    {
        var thisMonth = new DateOnly(runDate.Year, runDate.Month, 1);
        var end = thisMonth.AddDays(-1);
        return (new DateOnly(end.Year, end.Month, 1), end);
    }

    public static DateOnly GetCalendarQuarterStart(DateOnly date)
    {
        var firstMonth = ((date.Month - 1) / 3 * 3) + 1;
        return new DateOnly(date.Year, firstMonth, 1);
    }

    public static decimal RoundMoney(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
