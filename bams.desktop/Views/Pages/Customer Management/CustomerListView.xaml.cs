using System.Windows.Controls;

namespace bams.desktop.Views.Pages;

/// <summary>
/// Customer List page. Every binding is on CustomerListViewModel (Filter/Table); no code-behind logic.
/// </summary>
public partial class CustomerListView : UserControl
{
    public CustomerListView()
    {
        InitializeComponent();
    }
}
