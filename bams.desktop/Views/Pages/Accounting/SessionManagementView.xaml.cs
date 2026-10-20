using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using bams.desktop.ViewModels.Pages.Accounting;
namespace bams.desktop.Views.Pages.Accounting;
public partial class SessionManagementView : UserControl
{
    public SessionManagementView() => InitializeComponent();
    // Presentation-only hit testing prevents action buttons from triggering row navigation.
    private void OpenSessionRow(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element != null && element is not DataGridRow)
        {
            if (element is ButtonBase || element is ScrollBar) return;
            element = VisualTreeHelper.GetParent(element);
        }
        if (element is DataGridRow row && DataContext is SessionManagementViewModel vm) vm.DetailCommand.Execute(row.Item);
    }
}