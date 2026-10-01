namespace bams.server.Utils;

/// <summary>
/// The bank's business clock. Timestamps are stored in UTC, but every business date (posting and value dates,
/// daily and monthly limits, end-of-day, account-number periods, product effective dates) follows Myanmar time
/// (UTC+06:30, no daylight saving), so a posting at 05:00 local belongs to that local day, not the previous UTC day.
/// </summary>
public static class BusinessTime
{
    /// <summary>IANA id of the bank's time zone; resolvable on Windows and Linux through ICU.</summary>
    public const string TimeZoneId = "Asia/Rangoon";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    /// <summary>The current business date in Myanmar.</summary>
    public static DateOnly Today => ToBusinessDate(DateTime.UtcNow);

    /// <summary>Converts a UTC timestamp to Myanmar local time.</summary>
    public static DateTime ToBusinessDateTime(DateTime utcTimestamp)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcTimestamp, DateTimeKind.Utc), Zone);
    }

    /// <summary>Returns the Myanmar business date on which a UTC timestamp falls.</summary>
    public static DateOnly ToBusinessDate(DateTime utcTimestamp)
    {
        return DateOnly.FromDateTime(ToBusinessDateTime(utcTimestamp));
    }

    /// <summary>
    /// Returns the UTC instants bounding one Myanmar business day: <c>StartUtc</c> inclusive, <c>EndUtc</c> exclusive.
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) GetUtcRange(DateOnly businessDate)
    {
        return (ToUtc(businessDate), ToUtc(businessDate.AddDays(1)));
    }

    // Converts local midnight at the start of a business date to UTC.
    private static DateTime ToUtc(DateOnly businessDate)
    {
        var localMidnight = businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, Zone);
    }
}
