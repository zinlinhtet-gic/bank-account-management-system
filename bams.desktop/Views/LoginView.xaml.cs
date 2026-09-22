using System.Windows.Controls;

namespace bams.desktop.Views;

/// <summary>
/// Interaction logic for LoginView.xaml
/// </summary>
public partial class LoginView : UserControl
{
    private bool _isPasswordVisible;

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

    // Toggles password visibility between PasswordBox and TextBox.
    private void OnTogglePasswordClick(object sender, System.Windows.RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;

        if (DataContext is ViewModels.LoginViewModel viewModel)
        {
            if (_isPasswordVisible)
            {
                // Show TextBox, hide PasswordBox
                PasswordTextBox.Text = PasswordBox.Password;
                PasswordTextBox.Visibility = System.Windows.Visibility.Visible;
                PasswordBox.Visibility = System.Windows.Visibility.Collapsed;
                EyeIcon.Text = "👁";
            }
            else
            {
                // Show PasswordBox, hide TextBox
                PasswordBox.Password = PasswordTextBox.Text;
                PasswordBox.Visibility = System.Windows.Visibility.Visible;
                PasswordTextBox.Visibility = System.Windows.Visibility.Collapsed;
                EyeIcon.Text = "👁‍🗨";
            }
        }
    }
}