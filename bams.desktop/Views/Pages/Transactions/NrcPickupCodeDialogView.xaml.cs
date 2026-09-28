using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using bams.desktop.ViewModels.Pages.Transactions;

namespace bams.desktop.Views.Pages.Transactions;

/// <summary>
/// One-time pickup code display. Code-behind copies the code to the clipboard: the clipboard is a UI-only concern
/// and needs the WPF <see cref="Clipboard"/> API, which a ViewModel must not touch.
/// </summary>
public partial class NrcPickupCodeDialogView : UserControl
{
    public NrcPickupCodeDialogView()
    {
        InitializeComponent();
    }

    // Copies the code and confirms on the button itself.
    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not NrcPickupCodeViewModel viewModel)
        {
            return;
        }

        try
        {
            Clipboard.SetText(viewModel.PickupCode);
            CopyButton.Content = "Copied";
        }
        catch (COMException)
        {
            // Another app is holding the clipboard; the code is still on screen to read out.
        }
    }
}
