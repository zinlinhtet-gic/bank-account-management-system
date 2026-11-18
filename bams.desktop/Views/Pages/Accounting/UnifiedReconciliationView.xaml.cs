using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using bams.desktop.ViewModels.Pages.Accounting;
namespace bams.desktop.Views.Pages.Accounting;
public partial class UnifiedReconciliationView : UserControl
{
    public UnifiedReconciliationView() { InitializeComponent(); }
    // Hit testing is view-only; buttons retain their own commands.
    private void OpenRow(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? node = e.OriginalSource as DependencyObject;
        while (node != null && node is not DataGridRow)
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase) return;
            node = VisualTreeHelper.GetParent(node);
        }
        if (node is DataGridRow row && DataContext is UnifiedReconciliationViewModel vm) vm.DetailCommand.Execute(row.Item);
    }
    private void OpenRowKey(object sender, KeyEventArgs e)
    {
        if(e.Key == Key.Enter && sender is DataGrid grid && DataContext is UnifiedReconciliationViewModel vm && vm.AssignedOnly)
        { vm.DetailCommand.Execute(grid.SelectedItem); e.Handled=true; }
    }
}
