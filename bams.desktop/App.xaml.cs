using System.Net.Http;
using System.Windows;
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

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        AppLog.WriteError("Unhandled UI dispatcher exception; the application may close.", e.Exception);
        try
        {
            var message = "An unexpected error occurred. The application will try to return to the previous screen.\n\n" + e.Exception.Message;
            if (MainWindow is null)
            {
                MessageBox.Show(message, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                MessageBox.Show(MainWindow, message, "Application Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (MainWindow?.DataContext is MainViewModel mainViewModel &&
                mainViewModel.TryRecoverFromUnhandledException())
            {
                e.Handled = true;
            }
        }
        catch (Exception recoveryException)
        {
            AppLog.WriteError("Could not display or recover from the unhandled UI exception.", recoveryException);
            // Leave the original exception unhandled if recovery itself fails.
        }
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString());
        AppLog.WriteError($"Unhandled application exception. IsTerminating={e.IsTerminating}.", exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLog.WriteError("Unobserved task exception.", e.Exception);
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

        // Register ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<NavBarViewModel>();

        // Register Page ViewModels
        services.AddTransient<UserManagementViewModel>();
        services.AddTransient<CustomerManagementViewModel>();
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

