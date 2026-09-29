using bams.server.Models.Jobs;

namespace bams.server.Configuration;

/// <summary>Describes a recurring interval or local calendar-month schedule.</summary>
public sealed record JobSchedule
{
    private JobSchedule(ScheduledJobScheduleType scheduleType, TimeSpan? interval,
        int? dayOfMonth, TimeSpan? localTime, string timeZoneId)
    {
        ScheduleType = scheduleType;
        Interval = interval;
        DayOfMonth = dayOfMonth;
        LocalTime = localTime;
        TimeZoneId = timeZoneId;
    }

    public ScheduledJobScheduleType ScheduleType { get; }

    public TimeSpan? Interval { get; }

    public int? DayOfMonth { get; }

    public TimeSpan? LocalTime { get; }

    public string TimeZoneId { get; }

    internal bool Matches(ScheduledJob job)
    {
        return job.ScheduleType == ScheduleType && job.IntervalTicks == Interval?.Ticks &&
            job.DayOfMonth == DayOfMonth && job.LocalTime == LocalTime && job.TimeZoneId == TimeZoneId;
    }

    internal void ApplyTo(ScheduledJob job)
    {
        job.ScheduleType = ScheduleType;
        job.IntervalTicks = Interval?.Ticks;
        job.DayOfMonth = DayOfMonth;
        job.LocalTime = LocalTime;
        job.TimeZoneId = TimeZoneId;
    }

    /// <summary>Creates a recurring interval schedule.</summary>
    public static JobSchedule Every(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "A job interval must be positive.");
        }

        return new JobSchedule(ScheduledJobScheduleType.Interval, interval, null, null, "UTC");
    }

    /// <summary>Creates a monthly schedule at a local time and timezone.</summary>
    public static JobSchedule Monthly(int dayOfMonth, TimeSpan localTime, string timeZoneId)
    {
        if (dayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), "Day of month must be between 1 and 31.");
        }
        if (localTime < TimeSpan.Zero || localTime >= TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(localTime), "Local time must be within one day.");
        }
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            throw new ArgumentException("A timezone ID is required.", nameof(timeZoneId));
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return new JobSchedule(ScheduledJobScheduleType.Monthly, null, dayOfMonth, localTime, timeZoneId);
    }

    /// <summary>Calculates the next occurrence after the supplied UTC instant.</summary>
    public DateTime GetNextRunAtUtc(DateTime utcNow)
    {
        var utc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        if (ScheduleType == ScheduledJobScheduleType.Interval)
        {
            var intervalTicks = Interval!.Value.Ticks;
            var elapsedTicks = (utc - DateTime.UnixEpoch).Ticks;
            var nextTicks = checked((elapsedTicks / intervalTicks + 1) * intervalTicks);
            return DateTime.SpecifyKind(DateTime.UnixEpoch.AddTicks(nextTicks), DateTimeKind.Utc);
        }

        var zone = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(utc, zone);
        var candidate = new DateTime(localNow.Year, localNow.Month,
            Math.Min(DayOfMonth!.Value, DateTime.DaysInMonth(localNow.Year, localNow.Month)),
            0, 0, 0, DateTimeKind.Unspecified).Add(LocalTime!.Value);
        var candidateUtc = ConvertLocalTimeToUtc(candidate, zone);
        if (candidateUtc <= utc)
        {
            var nextMonth = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1);
            candidate = new DateTime(nextMonth.Year, nextMonth.Month,
                Math.Min(DayOfMonth.Value, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)),
                0, 0, 0, DateTimeKind.Unspecified).Add(LocalTime.Value);
            candidateUtc = ConvertLocalTimeToUtc(candidate, zone);
        }

        return candidateUtc;
    }

    // Moves invalid daylight-saving local times forward and consistently chooses the earlier ambiguous instant.
    private static DateTime ConvertLocalTimeToUtc(DateTime localTime, TimeZoneInfo zone)
    {
        while (zone.IsInvalidTime(localTime))
        {
            localTime = localTime.AddMinutes(1);
        }

        if (zone.IsAmbiguousTime(localTime))
        {
            var offset = zone.GetAmbiguousTimeOffsets(localTime).Max();
            return DateTime.SpecifyKind(localTime - offset, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(localTime, zone);
    }
}
