using System.Globalization;
using System.Windows.Data;
using bams.desktop.Utils;

namespace Bams.Desktop.Converters;

/// <summary>Renders nullable and non-null monetary values with the application's fixed money format.</summary>
public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null || value == System.Windows.DependencyProperty.UnsetValue
            ? string.Empty
            : System.Convert.ToDecimal(value, CultureInfo.InvariantCulture) is var amount
                ? MoneyText.Format(amount)
                : string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
