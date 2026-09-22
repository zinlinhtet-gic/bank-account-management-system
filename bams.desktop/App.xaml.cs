using System.Windows;
using bams.desktop.Services;
using bams.desktop.ViewModels;
using bams.desktop.ViewModels.Pages;
using Bams.Desktop.Components.NavBar;
using Microsoft.Extensions.DependencyInjection;

namespace bams.desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // Create and show main window
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register AuthContext instance (singleton pattern)
        services.AddSingleton(sp => AuthContext.Instance);

        // Register HTTP client with base URL
        services.AddHttpClient<Services.IAuthenticationService, Services.AuthenticationService>(client =>
        {
            client.BaseAddress = new Uri("http://localhost:5121");
        });

        // Register Navigation Service
        services.AddSingleton<Services.INavigationService, Services.NavigationService>();

        // Register ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<NavBarViewModel>();

        // Register Page ViewModels
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<CustomerManagementViewModel>();
        services.AddTransient<CustomerKYCViewModel>();
        services.AddTransient<AccountManagementViewModel>();
        services.AddTransient<TransactionsViewModel>();
        services.AddTransient<TransactionHistoryViewModel>();
        services.AddTransient<AccountingViewModel>();
        services.AddTransient<OperationsViewModel>();
        services.AddTransient<AuditViewModel>();
        services.AddTransient<ConfigurationsViewModel>();
        services.AddTransient<CustomerListViewModel>();

        // Register Views
        services.AddTransient<Views.LoginView>();

        // Register main window
        services.AddTransient<MainWindow>();
    }
}

