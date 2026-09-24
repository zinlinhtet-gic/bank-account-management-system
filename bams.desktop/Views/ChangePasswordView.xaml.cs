using System.Windows.Controls;
using bams.desktop.ViewModels;

namespace bams.desktop.Views;

/// <summary>
/// Forced password-change screen shown after a first sign-in. All behaviour is in
/// <see cref="ChangePasswordViewModel"/>; password fields use the PasswordInput component.
/// </summary>
public partial class ChangePasswordView : UserControl
{
    public ChangePasswordView(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
