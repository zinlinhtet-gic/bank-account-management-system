using System.Windows.Controls;

namespace bams.desktop.Views;

/// <summary>
/// Sign-in screen. All behaviour is in <see cref="ViewModels.LoginViewModel"/>;
/// password binding and the show/hide toggle are handled by the PasswordInput component.
/// </summary>
public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }
}
