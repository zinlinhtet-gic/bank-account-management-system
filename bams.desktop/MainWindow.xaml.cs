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

    public MainWindow(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        
        _serviceProvider = serviceProvider;
        _authContext = _serviceProvider.GetRequiredService<AuthContext>();

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
        
        // Show login view, hide main app
        LoginContentControl.Content = loginView;
        MainAppGrid.Visibility = Visibility.Collapsed;
    }

    private void ShowMainApplication()
    {
        // Create the main application view model with navigation
        var navBarViewModel = _serviceProvider.GetRequiredService<NavBarViewModel>();
        var mainViewModel = _serviceProvider.GetRequiredService<MainViewModel>();
        
        // Set the data context for the main window
        DataContext = mainViewModel;
        
        // Hide login view, show main app
        LoginContentControl.Content = null;
        MainAppGrid.Visibility = Visibility.Visible;
    }
}