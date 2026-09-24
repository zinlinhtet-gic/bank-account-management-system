using System.Windows;
using bams.desktop.Services;
using bams.desktop.ViewModels;
using bams.desktop.Views;
using Bams.Desktop.Components.NavBar;
using Microsoft.Extensions.DependencyInjection;

namespace bams.desktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly IServiceProvider _serviceProvider;
    private readonly AuthContext _authContext;
    private readonly IAuthenticationService _authenticationService;
    private readonly IDialogService _dialogService;

    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _serviceProvider = serviceProvider;
        _authContext = _serviceProvider.GetRequiredService<AuthContext>();
        _authenticationService = _serviceProvider.GetRequiredService<IAuthenticationService>();
        _dialogService = _serviceProvider.GetRequiredService<IDialogService>();

        // Show login view on startup
        ShowLoginView();
    }

    private void ShowLoginView()
    {
        var loginViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
        var loginView = _serviceProvider.GetRequiredService<LoginView>();
        loginView.DataContext = loginViewModel;
        
        // Subscribe to login success event
        loginViewModel.OnLoginSuccess += ShowMainApplication;
        
        // Subscribe to password change required event
        loginViewModel.OnPasswordChangeRequired += ShowChangePasswordView;
        
        // Show login view, hide main app
        LoginContentControl.Content = loginView;
        ChangePasswordContentControl.Visibility = Visibility.Collapsed;
        MainAppGrid.Visibility = Visibility.Collapsed;
    }

    private void ShowChangePasswordView()
    {
        var changePasswordViewModel = _serviceProvider.GetRequiredService<ChangePasswordViewModel>();
        var changePasswordView = new ChangePasswordView(changePasswordViewModel);
        
        // Subscribe to password change success event
        changePasswordViewModel.OnPasswordChangeSuccess += ShowMainApplication;
        
        // Show change password view, hide login and main app
        ChangePasswordContentControl.Content = changePasswordView;
        ChangePasswordContentControl.Visibility = Visibility.Visible;
        LoginContentControl.Content = null;
        MainAppGrid.Visibility = Visibility.Collapsed;
    }

    private void ShowMainApplication()
    {
        // Create the main application view model with navigation
        var navBarViewModel = _serviceProvider.GetRequiredService<NavBarViewModel>();
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();

        mainViewModel.OnLogoutRequested += HandleLogoutRequested;
        
        // Set the data context for the main window
        DataContext = mainViewModel;
        
        // Hide login view, show main app
        LoginContentControl.Content = null;
        MainAppGrid.Visibility = Visibility.Visible;
    }

    // Asks the user to confirm, then clears the session and returns to the sign-in screen.
    private void HandleLogoutRequested()
    {
        var confirmed = _dialogService.Confirm(new ConfirmDialogOptions(
            Title: "Log out of DaLanBank?",
            Message: "You will need to sign in again to continue. Anything you have not saved on this page will be lost.",
            ConfirmText: "Log out",
            CancelText: "Stay signed in",
            IsDestructive: true,
            Icon: (System.Windows.Media.Geometry)FindResource("Icon.Logout")));

        if (!confirmed)
        {
            return;
        }

        _authenticationService.ClearAuthToken();
        _authContext.ClearSession();
        ShowLoginView();
    }
}