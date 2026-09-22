using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace Bams.Desktop.Components.Header;

public partial class Header : UserControl
{
    public static readonly DependencyProperty SelectedTitleProperty =
        DependencyProperty.Register(
            nameof(SelectedTitle),
            typeof(string),
            typeof(Header),
            new PropertyMetadata(string.Empty));

    public Header()
    {
        InitializeComponent();
    }

    public string SelectedTitle
    {
        get => (string)GetValue(SelectedTitleProperty);
        set => SetValue(SelectedTitleProperty, value);
    }

    public string CurrentDate => DateTime.Now.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture);
}
