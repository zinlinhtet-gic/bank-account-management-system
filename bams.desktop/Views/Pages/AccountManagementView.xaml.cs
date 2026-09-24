using System.Windows.Controls;
using Microsoft.Win32;

namespace bams.desktop.Views.Pages;

public partial class AccountManagementView : UserControl
{
    public AccountManagementView()
    {
        InitializeComponent();
    }

    private void BrowseDocument_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            CheckFileExists = true,
            Multiselect = false,
            Title = "Choose account document"
        };

        if (dialog.ShowDialog() == true && DataContext is ViewModels.Pages.AccountManagementViewModel viewModel)
        {
            viewModel.SelectedDocumentPath = dialog.FileName;
        }
    }
}
