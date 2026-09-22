using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Bams.Desktop.Converters;

public class ActiveToVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values[0]?.ToString() == values[1]?.ToString()
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}