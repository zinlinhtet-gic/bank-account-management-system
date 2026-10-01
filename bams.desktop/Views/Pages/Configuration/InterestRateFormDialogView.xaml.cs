using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using bams.desktop.ViewModels.Pages.Configuration;

namespace bams.desktop.Views.Pages.Configuration;

/// <summary>
/// Create / edit interest rate rule form. Code-behind only sets the header icon and the initial
/// keyboard focus, which are view concerns.
/// </summary>
public partial class InterestRateFormDialogView : UserControl
{
    public InterestRateFormDialogView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is InterestRateFormViewModel { IsEditMode: true })
        {
            HeaderIcon.Geometry = (Geometry)FindResource("Icon.Edit");
        }

        AccountTypeCombo.Focus();
    }
}
