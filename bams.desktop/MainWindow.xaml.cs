using System.Windows;
using bams.desktop.Services;
using bams.desktop.ViewModels;
using bams.desktop.Views;
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
        
        MainContentControl.Content = loginView;
    }

    private void ShowMainApplication()
    {
        // For now, show a simple main application view
        // In production, this would navigate to the main banking application
        var mainAppViewModel = new MainAppViewModel(_authContext);
        
        // Create a simple content for demonstration
        var mainContent = new System.Windows.Controls.StackPanel
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = System.Windows.VerticalAlignment.Center
        };
        
        mainContent.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = mainAppViewModel.WelcomeMessage,
            FontSize = 32,
            FontWeight = System.Windows.FontWeights.Bold,
            Margin = new System.Windows.Thickness(0, 0, 0, 20)
        });
        
        mainContent.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = mainAppViewModel.UserRole,
            FontSize = 18,
            Margin = new System.Windows.Thickness(0, 0, 0, 10)
        });
        
        mainContent.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Main Application - Implement your banking UI here",
            FontSize = 24,
            Margin = new System.Windows.Thickness(0, 20, 0, 0)
        });
        
        MainContentControl.Content = mainContent;
    }
}