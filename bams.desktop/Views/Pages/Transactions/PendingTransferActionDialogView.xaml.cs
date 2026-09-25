using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using bams.desktop.ViewModels.Pages.Transactions;

namespace bams.desktop.Views.Pages.Transactions;

/// <summary>
/// Pending transfer action form. Code-behind only switches to the danger look for refunding actions, the same
/// visual rule the confirmation dialog applies; it is purely a view concern.
/// </summary>
public partial class PendingTransferActionDialogView : UserControl
{
    public PendingTransferActionDialogView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    // Red icon and solid red button for cancel / fail, which refund the sender and cannot be undone.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PendingTransferActionViewModel { IsDestructive: true })
        {
            return;
        }

        IconCircle.Fill = (Brush)FindResource("DangerSoftBrush");
        HeaderIcon.Foreground = (Brush)FindResource("DangerBrush");
        SubmitButton.Style = (Style)FindResource("Button.DangerSolid");
    }
}
