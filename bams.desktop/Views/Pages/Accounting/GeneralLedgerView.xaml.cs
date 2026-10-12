using System.Windows.Controls;
using bams.desktop.ViewModels.Pages.Accounting;

namespace bams.desktop.Views.Pages.Accounting;

/// <summary>
/// Interaction logic for GeneralLedgerView.xaml.
/// Business and loading logic remains in GeneralLedgerViewModel.
/// </summary>
public partial class GeneralLedgerView : UserControl
{
    public GeneralLedgerView()
    {
        InitializeComponent();
    }

    private void AccountsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not GeneralLedgerViewModel viewModel || e.AddedItems.Count == 0)
        {
            return;
        }

        viewModel.OpenAccountDetailCommand.Execute(e.AddedItems[0]);
    }
}
