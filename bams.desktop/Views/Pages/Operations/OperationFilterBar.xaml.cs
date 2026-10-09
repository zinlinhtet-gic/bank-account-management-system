using System.Windows;
using System.Windows.Controls;

namespace bams.desktop.Views.Pages.Operations;

/// <summary>
/// Interaction logic for OperationFilterBar.xaml. Only layout properties live here; filtering is in the ViewModel.
/// </summary>
public partial class OperationFilterBar : UserControl
{
    public static readonly DependencyProperty ExtraFilterProperty = DependencyProperty.Register(
        nameof(ExtraFilter), typeof(object), typeof(OperationFilterBar));

    public static readonly DependencyProperty FromLabelProperty = DependencyProperty.Register(
        nameof(FromLabel), typeof(string), typeof(OperationFilterBar), new PropertyMetadata("FROM"));

    public static readonly DependencyProperty ToLabelProperty = DependencyProperty.Register(
        nameof(ToLabel), typeof(string), typeof(OperationFilterBar), new PropertyMetadata("TO"));

    public OperationFilterBar()
    {
        InitializeComponent();
    }

    /// <summary>Optional page-specific field shown after the search box.</summary>
    public object? ExtraFilter
    {
        get => GetValue(ExtraFilterProperty);
        set => SetValue(ExtraFilterProperty, value);
    }

    /// <summary>Label above the start date, e.g. "PERIOD FROM" or "MATURES FROM".</summary>
    public string FromLabel
    {
        get => (string)GetValue(FromLabelProperty);
        set => SetValue(FromLabelProperty, value);
    }

    /// <summary>Label above the end date.</summary>
    public string ToLabel
    {
        get => (string)GetValue(ToLabelProperty);
        set => SetValue(ToLabelProperty, value);
    }
}
