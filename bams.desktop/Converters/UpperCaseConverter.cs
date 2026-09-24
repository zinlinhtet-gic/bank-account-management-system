using System.Globalization;
using System.Windows.Data;

namespace Bams.Desktop.Converters;

/// <summary>
/// Shows text in capitals (WPF has no CSS-style text-transform). Used for table headers and labels.
/// Non-text values are passed through unchanged.
/// </summary>
public sealed class UpperCaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string text ? text.ToUpper(culture) : value;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
