using System.Windows;
using bams.desktop.Constants;
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
    private readonly IDialogService _dialogService;
    private readonly ISessionService _sessionService;

    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _serviceProvider = serviceProvider;
        _dialogService = _serviceProvider.GetRequiredService<IDialogService>();
        _sessionService = _serviceProvider.GetRequiredService<ISessionService>();

        // Whoever ends the session (logout button, self-delete...), the window returns to sign-in.
        _sessionService.SessionEnded += ShowLoginView;

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
        
        // Hide the login and change-password views (the latter stays behind after a first login otherwise
        // and shows through any page area without its own background), then show the main app.
        LoginContentControl.Content = null;
        ChangePasswordContentControl.Content = null;
        ChangePasswordContentControl.Visibility = Visibility.Collapsed;
        MainAppGrid.Visibility = Visibility.Visible;
    }

    // Asks the user to confirm, then ends the session (SessionEnded shows the sign-in screen).
    private void HandleLogoutRequested()
    {
        var confirmed = _dialogService.Confirm(new ConfirmDialogOptions(
            Title: $"Log out of {BrandConstants.BankShortName}?",
            Message: "You will need to sign in again to continue. Anything you have not saved on this page will be lost.",
            ConfirmText: "Log out",
            CancelText: "Stay signed in",
            IsDestructive: true,
            Icon: (System.Windows.Media.Geometry)FindResource("Icon.Logout")));

        if (!confirmed)
        {
            return;
        }

        _sessionService.EndSession();
    }
}