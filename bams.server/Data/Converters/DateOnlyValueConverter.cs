using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace bams.server.Data.Converters;

/// <summary>
/// Converts non-nullable <see cref="DateOnly"/> values to the <see cref="DateTime"/>
/// representation returned by the MySQL provider for database DATE columns.
/// </summary>
public sealed class DateOnlyValueConverter : ValueConverter<DateOnly, DateTime>
{
    public DateOnlyValueConverter()
        : base(
            date => date.ToDateTime(TimeOnly.MinValue),
            dateTime => DateOnly.FromDateTime(dateTime))
    {
    }
}

/// <summary>
/// Converts nullable <see cref="DateOnly"/> values to nullable provider dates.
/// </summary>
public sealed class NullableDateOnlyValueConverter : ValueConverter<DateOnly?, DateTime?>
{
    public NullableDateOnlyValueConverter()
        : base(
            date => date.HasValue
                ? date.Value.ToDateTime(TimeOnly.MinValue)
                : null,
            dateTime => dateTime.HasValue
                ? DateOnly.FromDateTime(dateTime.Value)
                : null)
    {
    }
}
