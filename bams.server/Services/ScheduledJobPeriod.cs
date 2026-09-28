using bams.server.Services.Jobs;

namespace bams.server.Services;

internal static class ScheduledJobPeriod
{
    public const string TimeZoneId = "Asia/Rangoon";

    public static DateOnly GetRunDate(ScheduledJobExecutionContext context)
    {
        var scheduledUtc = DateTime.SpecifyKind(context.ScheduledForUtc, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            scheduledUtc, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId)));
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
