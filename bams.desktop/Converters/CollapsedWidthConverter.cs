using System.Globalization;
using System.Windows.Data;

namespace Bams.Desktop.Converters;

public class CollapsedWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => (bool)value ? 63d : 220d;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}