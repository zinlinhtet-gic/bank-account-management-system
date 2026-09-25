using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using bams.desktop.DTOs.Accounts;
using bams.desktop.ViewModels.Pages;

namespace bams.desktop.Views.Pages;

public partial class AccountListView : UserControl
{
    public AccountListView() => InitializeComponent();

    // Ignore clicks originating from row action buttons; other row clicks open the account profile.
    private void AccountRow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRow row || row.DataContext is not AccountSummaryResponse account ||
            DataContext is not AccountListPageViewModel page)
            return;
        for (DependencyObject? current = e.OriginalSource as DependencyObject; current is not null && current != row; current = VisualTreeHelper.GetParent(current))
            if (current is ButtonBase) return;
        e.Handled = true;
        _ = page.Workflow.OpenAccountCommand.ExecuteAsync(account);
    }
}
