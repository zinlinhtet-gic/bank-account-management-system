using System.Windows.Controls;

namespace bams.desktop.Views.Pages;

/// <summary>
/// Generic page view that can be used for all placeholder pages.
/// Displays the page title and description from the ViewModel.
/// </summary>
public partial class GenericPageView : UserControl
{
    public GenericPageView()
    {
        InitializeComponent();
    }
}