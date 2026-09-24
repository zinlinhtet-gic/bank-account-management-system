namespace bams.desktop.Utils;

/// <summary>
/// Converts server timestamps (UTC) for display.
/// </summary>
public static class DateTimeDisplay
{
    /// <summary>
    /// Returns the local time for a server timestamp. Values read from the database arrive without a "Z",
    /// so an unspecified kind is treated as UTC, which is how the server stores every timestamp.
    /// </summary>
    public static DateTime ToLocal(DateTime serverTimestamp)
    {
        return serverTimestamp.Kind == DateTimeKind.Local
            ? serverTimestamp
            : DateTime.SpecifyKind(serverTimestamp, DateTimeKind.Utc).ToLocalTime();
    }

    /// <summary>
    /// Returns the instant a local calendar day starts, for date filters sent to the server.
    /// </summary>
    public static DateTimeOffset StartOfLocalDay(DateTime date)
    {
        return new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Local));
    }
}
