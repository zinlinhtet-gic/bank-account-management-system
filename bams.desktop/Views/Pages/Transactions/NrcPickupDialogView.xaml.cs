using System.Windows;
using System.Windows.Controls;

namespace bams.desktop.Views.Pages.Transactions;

/// <summary>
/// NRC pickup form. Code-behind only sets the initial keyboard focus, which is a view concern.
/// </summary>
public partial class NrcPickupDialogView : UserControl
{
    public NrcPickupDialogView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    // Puts the caret in the first field so the officer can type the collector's name right away.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ReceiverNameBox.Focus();
    }
}
