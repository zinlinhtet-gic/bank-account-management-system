using System.Windows.Controls;

namespace bams.desktop.Views.Pages.Transactions;

/// <summary>
/// Account type and account choice shared by the source and destination sections of the transaction form.
/// All behaviour lives in AccountSelectionViewModel.
/// </summary>
public partial class AccountSelectionView : UserControl
{
    public AccountSelectionView()
    {
        InitializeComponent();
    }
}
