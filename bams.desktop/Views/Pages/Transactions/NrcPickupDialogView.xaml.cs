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

    // Puts the caret in the code box so the officer can type the code right away.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PickupCodeBox.Focus();
    }
}
