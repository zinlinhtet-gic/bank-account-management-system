using System.Windows.Controls;

namespace Bams.Desktop.Components.NavBar;

public partial class NavBar : UserControl
{
    public NavBar()
    {
        InitializeComponent();
        DataContext = new NavBarViewModel();
    }

    public NavBarViewModel ViewModel => (NavBarViewModel)DataContext;
}