using System.Windows.Controls;
using bams.desktop.ViewModels.Pages;
using Microsoft.Win32;

namespace bams.desktop.Views.Pages;

public partial class AccountCreateView : UserControl
{
    public AccountCreateView() => InitializeComponent();

    private void BrowseRequiredDocument_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: AccountDocumentInputViewModel input }) return;
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Title = input.IsPhoto ? "Choose customer photo" : "Choose account document",
            Filter = input.IsPhoto ? "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png" : "Supported documents (*.pdf;*.jpg;*.jpeg;*.png)|*.pdf;*.jpg;*.jpeg;*.png"
        };
        if (dialog.ShowDialog() == true) input.SetFilePath(dialog.FileName);
    }
}
