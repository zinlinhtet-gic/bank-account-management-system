namespace bams.desktop.Utils;

/// <summary>
/// Converts server UTC timestamps to Myanmar time for views that require a fixed bank time zone.
/// </summary>
public static class MyanmarTimeDisplay
{
    private static readonly TimeZoneInfo MyanmarTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Rangoon");

    /// <summary>
    /// Converts a server timestamp to Myanmar local time. Unspecified values from the API are treated as UTC.
    /// </summary>
    public static DateTime ToMyanmarTime(DateTime serverTimestamp)
    {
        var utcTimestamp = serverTimestamp.Kind == DateTimeKind.Local
            ? serverTimestamp.ToUniversalTime()
            : DateTime.SpecifyKind(serverTimestamp, DateTimeKind.Utc);

        return TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, MyanmarTimeZone);
    }
}
