using System.Windows;
using System.Windows.Controls;
using bams.desktop.ViewModels.Pages.Users;

namespace bams.desktop.Views.Pages.Users;

/// <summary>
/// Read-only user detail card. Code-behind only picks the status badge colour, a view concern.
/// </summary>
public partial class UserDetailsDialogView : UserControl
{
    public UserDetailsDialogView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    // Active is green; any other status (Disabled) is red.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserDetailsViewModel { IsActive: false })
        {
            StatusBadge.Style = (Style)FindResource("Badge.Danger");
        }
    }
}
