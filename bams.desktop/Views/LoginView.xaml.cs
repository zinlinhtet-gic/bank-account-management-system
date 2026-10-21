using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

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
        Loaded += OnLoaded;
    }

    // FocusManager.FocusedElement only sets logical focus. After logout, keyboard focus is still on the hidden shell,
    // so move it to the username box once the view is shown; Input priority runs after the window has re-activated.
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Input, () => Keyboard.Focus(UsernameBox));
    }
}
