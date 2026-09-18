using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Bams.Desktop.Converters;

public class MenuIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => Application.Current.Resources[(bool)value ? "Icon.Menu" : "Icon.MenuNarrow"];

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}