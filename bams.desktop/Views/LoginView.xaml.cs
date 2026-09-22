using System.Windows.Controls;

namespace bams.desktop.Views;

/// <summary>
/// Interaction logic for LoginView.xaml
/// </summary>
public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    // Updates the ViewModel's password property when the PasswordBox changes.
    private void OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.LoginViewModel viewModel)
        {
            viewModel.Password = PasswordBox.Password;
        }
    }
}