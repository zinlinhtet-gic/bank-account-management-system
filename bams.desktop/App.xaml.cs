using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.Services;
using bams.desktop.ViewModels;
using bams.desktop.ViewModels.Pages;
using bams.desktop.ViewModels.Pages.Configuration;
using bams.desktop.Utils;
using Bams.Desktop.Components.NavBar;
using Microsoft.Extensions.DependencyInjection;
using bams.desktop.ViewModels.Pages.Accounting;
using bams.desktop.ViewModels.Pages.Audit;

namespace bams.desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    [ThreadStatic]
    private static bool _handlingFirstChanceException;

    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Set true here to enable daily desktop diagnostic logs.
        Properties["Debug Log"] = true;

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
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

    private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        if (_handlingFirstChanceException ||
            !AppLog.IsEnabled ||
            e.Exception is OperationCanceledException)
        {
            return;
        }

        var stackTrace = e.Exception.StackTrace;
        if (stackTrace is null ||
            (!stackTrace.Contains("bams.desktop.ViewModels.", StringComparison.Ordinal) &&
             !stackTrace.Contains("Bams.Desktop.Components.NavBar.NavBarViewModel", StringComparison.Ordinal)))
        {
            return;
        }

        try
        {
            _handlingFirstChanceException = true;
            AppLog.WriteError("Exception thrown while executing view-model code.", e.Exception);
        }
        catch (Exception loggingException)
        {
            // Diagnostic exception handling must never replace the application's original exception.
            System.Diagnostics.Debug.WriteLine($"Could not log a view-model exception: {loggingException}");
        }
        finally
        {
            _handlingFirstChanceException = false;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred.\n\n{e.Exception.Message}",
            "Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            AppLog.WriteError($"Unhandled exception. IsTerminating={e.IsTerminating}.", exception);
        }
        else
        {
            AppLog.WriteError(
                $"Unhandled non-exception object. IsTerminating={e.IsTerminating}.",
                new InvalidOperationException(e.ExceptionObject?.ToString() ?? "The runtime supplied a null exception object."));
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        AppLog.WriteError("An unobserved task exception was raised.", e.Exception);
        e.SetObserved();
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

        // Other Banks (view only)
        services.AddSingleton<Services.IOtherBankService, Services.OtherBankService>();

        // Interest Rate
        services.AddSingleton<Services.IInterestRateService, Services.InterestRateService>();

        // Fee Rate
        services.AddSingleton<Services.IFeeRateService, Services.FeeRateService>();

        // Bank Policies
        services.AddSingleton<Services.IBankPolicyService, Services.BankPolicyService>();

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
        services.AddTransient<AccountingEntriesViewModel>();
        // Each navigation gets fresh account-management UI state instead of reusing a stale singleton view tree.
        services.AddTransient<AccountManagementViewModel>();
        services.AddTransient<TransactionsViewModel>();
        services.AddTransient<TransactionHistoryViewModel>();
        services.AddTransient<TransactionAuditViewModel>();
        services.AddTransient<GeneralLedgerViewModel>();
        services.AddTransient<GLAccountDetailViewModel>();
        services.AddTransient<ReconciliationViewModel>();
        services.AddTransient<OperationsViewModel>();
        services.AddTransient<AuditViewModel>();
        services.AddTransient<ConfigurationsViewModel>();
        services.AddTransient<CustomerListViewModel>();

        // Register Configuration sub-pages
        services.AddTransient<InterestRateViewModel>();
        services.AddTransient<FeeRateViewModel>();
        services.AddTransient<BankPoliciesViewModel>();
        services.AddTransient<OtherBanksViewModel>();

        // Register Views
        services.AddTransient<Views.LoginView>();
        services.AddTransient<Views.ChangePasswordView>();

        // Register main window
        services.AddTransient<MainWindow>();

        // Register Accounting Service
        services.AddScoped<IAccountingService, AccountingService>();

    }
}

