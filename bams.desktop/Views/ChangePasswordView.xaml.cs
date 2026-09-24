using System.Windows;
using System.Windows.Controls;
using bams.desktop.ViewModels;

namespace bams.desktop.Views;

/// <summary>
/// Interaction logic for ChangePasswordView.xaml
/// </summary>
public partial class ChangePasswordView : UserControl
{
    private readonly ChangePasswordViewModel _viewModel;
    private bool _isCurrentPasswordVisible;
    private bool _isNewPasswordVisible;
    private bool _isConfirmPasswordVisible;

    public ChangePasswordView(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void OnCurrentPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.CurrentPassword = passwordBox.Password;
        }
    }

    private void OnNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.NewPassword = passwordBox.Password;
        }
    }

    private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.ConfirmPassword = passwordBox.Password;
        }
    }

    private void OnToggleCurrentPasswordClick(object sender, RoutedEventArgs e)
    {
        _isCurrentPasswordVisible = !_isCurrentPasswordVisible;

        if (_isCurrentPasswordVisible)
        {
            CurrentPasswordTextBox.Text = CurrentPasswordBox.Password;
            CurrentPasswordTextBox.Visibility = System.Windows.Visibility.Visible;
            CurrentPasswordBox.Visibility = System.Windows.Visibility.Collapsed;
            CurrentEyeIcon.Text = "👁";
        }
        else
        {
            CurrentPasswordBox.Password = CurrentPasswordTextBox.Text;
            CurrentPasswordBox.Visibility = System.Windows.Visibility.Visible;
            CurrentPasswordTextBox.Visibility = System.Windows.Visibility.Collapsed;
            CurrentEyeIcon.Text = "👁‍🗨";
        }
    }

    private void OnToggleNewPasswordClick(object sender, RoutedEventArgs e)
    {
        _isNewPasswordVisible = !_isNewPasswordVisible;

        if (_isNewPasswordVisible)
        {
            NewPasswordTextBox.Text = NewPasswordBox.Password;
            NewPasswordTextBox.Visibility = System.Windows.Visibility.Visible;
            NewPasswordBox.Visibility = System.Windows.Visibility.Collapsed;
            NewEyeIcon.Text = "👁";
        }
        else
        {
            NewPasswordBox.Password = NewPasswordTextBox.Text;
            NewPasswordBox.Visibility = System.Windows.Visibility.Visible;
            NewPasswordTextBox.Visibility = System.Windows.Visibility.Collapsed;
            NewEyeIcon.Text = "👁‍🗨";
        }
    }

    private void OnToggleConfirmPasswordClick(object sender, RoutedEventArgs e)
    {
        _isConfirmPasswordVisible = !_isConfirmPasswordVisible;

        if (_isConfirmPasswordVisible)
        {
            ConfirmPasswordTextBox.Text = ConfirmPasswordBox.Password;
            ConfirmPasswordTextBox.Visibility = System.Windows.Visibility.Visible;
            ConfirmPasswordBox.Visibility = System.Windows.Visibility.Collapsed;
            ConfirmEyeIcon.Text = "👁";
        }
        else
        {
            ConfirmPasswordBox.Password = ConfirmPasswordTextBox.Text;
            ConfirmPasswordBox.Visibility = System.Windows.Visibility.Visible;
            ConfirmPasswordTextBox.Visibility = System.Windows.Visibility.Collapsed;
            ConfirmEyeIcon.Text = "👁‍🗨";
        }
    }
}
