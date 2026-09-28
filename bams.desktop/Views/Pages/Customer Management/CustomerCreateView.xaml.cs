using System.Windows.Controls;

namespace bams.desktop.Views.Pages;

/// <summary>
/// Create Customer page. DataContext: CustomerCreateViewModel. Every field, the NRC/Passport
/// toggle, and the Save/Cancel flow are bound; no code-behind logic.
/// </summary>
public partial class CustomerCreateView : UserControl
{
    public CustomerCreateView()
    {
        InitializeComponent();
    }
}
