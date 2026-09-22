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
}
