using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using bams.desktop.Api;
using bams.desktop.Constants;
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

        // Final safety net: log the real exception and show a message instead of letting WPF
        // crash the process with no diagnostic trace (see AGENTS-WPF.md section 44).
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // Create and show main window
        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    // Catches any exception a ViewModel or Service did not handle itself. Logs it and shows a
    // generic message rather than letting the process die with no diagnostic trace.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred.\n\n{e.Exception.Message}",
            "Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register AuthContext instance (singleton pattern)
        services.AddSingleton(sp => AuthContext.Instance);

        // Register HTTP client with base URL (singleton to share auth token across app)
        services.AddSingleton<HttpClient>(sp => new HttpClient { BaseAddress = new Uri(ApiConstants.ServerBaseAddress) });
        services.AddSingleton<ApiClient>();
        services.AddSingleton<Services.IAuthenticationService, Services.AuthenticationService>();

        // Register Navigation Service
        services.AddSingleton<Services.INavigationService, Services.NavigationService>();

        // Themed confirmation dialogs (use instead of MessageBox.Show)
        services.AddSingleton<Services.IDialogService, Services.DialogService>();

        // Ends the session from anywhere (logout, self-delete); MainWindow returns to sign-in
        services.AddSingleton<Services.ISessionService, Services.SessionService>();

        // User Management
        services.AddSingleton<Services.IUserService, Services.UserService>();
        services.AddTransient<ViewModels.Pages.Users.UserFilterViewModel>();
        services.AddTransient<ViewModels.Pages.Users.UserListViewModel>();

        // Customer Management
        services.AddSingleton<Services.ICustomerService, Services.CustomerService>();
        services.AddTransient<ViewModels.Pages.Customers.CustomerFilterViewModel>();
        services.AddTransient<ViewModels.Pages.Customers.CustomerTableViewModel>();
        services.AddTransient<ViewModels.Pages.Customers.CustomerCreateViewModel>();

        // Register ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<NavBarViewModel>();

        // Register Page ViewModels
        services.AddTransient<UserManagementViewModel>();
        // services.AddTransient<CustomerManagementViewModel>();
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
        services.AddTransient<Views.ChangePasswordView>();

        // Register main window
        services.AddTransient<MainWindow>();
    }
}

