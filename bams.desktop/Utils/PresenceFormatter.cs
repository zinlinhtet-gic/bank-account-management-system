using bams.desktop.Constants;

namespace bams.desktop.Utils;

/// <summary>
/// Turns a last-seen time into text such as "Active now", "Last seen 12 min ago" or "Never signed in".
/// </summary>
public static class PresenceFormatter
{
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;

    // Beyond this, a relative time ("9 days ago") reads worse than the date itself.
    private const int MaximumRelativeDays = 7;

    /// <summary>
    /// Formats presence for display.
    /// </summary>
    /// <param name="lastSeenLocal">Last login or heartbeat in local time; null if the user never signed in.</param>
    /// <param name="isOnline">The server's online verdict.</param>
    public static string FormatLastSeen(DateTime? lastSeenLocal, bool isOnline)
    {
        if (isOnline)
        {
            return "Active now";
        }

        if (lastSeenLocal is null)
        {
            return "Never signed in";
        }

        var elapsed = DateTime.Now - lastSeenLocal.Value;

        if (elapsed.TotalMinutes < 1)
        {
            return "Last seen just now";
        }

        if (elapsed.TotalMinutes < MinutesPerHour)
        {
            return $"Last seen {(int)elapsed.TotalMinutes} min ago";
        }

        if (elapsed.TotalHours < HoursPerDay)
        {
            var hours = (int)elapsed.TotalHours;
            return hours == 1 ? "Last seen 1 hour ago" : $"Last seen {hours} hours ago";
        }

        if (elapsed.TotalDays < MaximumRelativeDays)
        {
            var days = (int)elapsed.TotalDays;
            return days == 1 ? "Last seen yesterday" : $"Last seen {days} days ago";
        }

        return $"Last seen {lastSeenLocal.Value.ToString(DisplayFormats.Date)}";
    }
}
