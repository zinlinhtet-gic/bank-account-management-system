using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Bams.Desktop.Converters;

public class IconKeyToGeometryConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Application.Current.Resources[value as string] ?? Geometry.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}