using System.Windows;
using System.Windows.Controls;

namespace bams.desktop.Views.Pages.Transactions;

/// <summary>
/// New transaction form shown under the Transactions page tabs. Code-behind only sets the initial keyboard
/// focus, which is a view concern.
/// </summary>
public partial class TransactionFormView : UserControl
{
    public TransactionFormView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    // Puts the caret in the amount so the officer can type it right after choosing the account.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AmountBox.Focus();
    }
}
