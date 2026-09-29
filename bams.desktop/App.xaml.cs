using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.Services;
using bams.desktop.ViewModels;
using bams.desktop.ViewModels.Pages;
using bams.desktop.Utils;
using Bams.Desktop.Components.NavBar;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;

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

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppLog.WriteInformation($"Application starting. Log file: {AppLog.LogFilePath}");

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
        services.AddSingleton<Services.IAccountManagementService, Services.AccountManagementService>();

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
        // Transactions (Transactions and Transaction History pages share the filter and list components)
        services.AddSingleton<Services.ITransactionService, Services.TransactionService>();
        services.AddSingleton<Services.IAccountService, Services.AccountService>();
        services.AddTransient<ViewModels.Pages.Transactions.TransactionFilterViewModel>();
        services.AddTransient<ViewModels.Pages.Transactions.TransactionListViewModel>();
        services.AddTransient<ViewModels.Pages.Transactions.TransactionDetailsLauncher>();

        // Register ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<NavBarViewModel>();

        // Register Page ViewModels
        services.AddTransient<UserManagementViewModel>();
        // services.AddTransient<CustomerManagementViewModel>();
        services.AddTransient<CustomerKYCViewModel>();
        // Each navigation gets fresh account-management UI state instead of reusing a stale singleton view tree.
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

