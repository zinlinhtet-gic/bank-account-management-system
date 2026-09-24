using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Bams.Desktop.Converters;

/// Shows an element only when the bound count is zero (e.g. an empty-state message under a grid).
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
