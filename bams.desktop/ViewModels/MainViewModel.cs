using bams.desktop.Constants;
using System.Windows.Input;
using bams.desktop.Commands;
using bams.desktop.Services;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages;
using Bams.Desktop.Components.NavBar;

namespace bams.desktop.ViewModels;

/// <summary>
/// Main ViewModel that coordinates the application shell with navigation.
/// Manages the NavBar, content area, and overall application state.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly AuthContext _authContext;
    private readonly INavigationService _navigationService;
    private object? _currentPage;
    private object? _previousPage;
    private string _activeItem = string.Empty;
    private string _currentPageLabel = string.Empty;
    private string _previousPageLabel = string.Empty;
    private CancellationTokenSource? _pageInitializationCancellation;

    public MainViewModel(AuthContext authContext, INavigationService navigationService, NavBarViewModel navBarViewModel)
    {
        _authContext = authContext;
        _navigationService = navigationService;
        NavBar = navBarViewModel;
        
        // Wire up navigation from NavBar
        NavBar.NavigateCommand = new RelayCommand(NavigateToPage);
        LogoutCommand = new RelayCommand(_ => OnLogoutRequested?.Invoke());
        
        // Initialize with user info from AuthContext
        UpdateUserInfo();
        
        // Load initial page
        NavigateToPage(GetDefaultPageForRole());
    }

    public NavBarViewModel NavBar { get; }

    public RelayCommand LogoutCommand { get; }

    public event Action? OnLogoutRequested;

    public object? CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(HasCurrentPage));
            }
        }
    }

    public bool HasCurrentPage => CurrentPage != null;

    public string ActiveItem
    {
        get => _activeItem;
        private set
        {
            if (SetProperty(ref _activeItem, value))
            {
                NavBar.ActiveItem = value;
            }
        }
    }

    /// <summary>
    /// Navigates to a page based on the navigation item label.
    /// </summary>
    private void NavigateToPage(object? parameter)
    {
        if (parameter == null)
            return;

        var pageLabel = parameter.ToString();
        if (string.IsNullOrEmpty(pageLabel))
            return;

        var viewModel = _navigationService.GetPageViewModel(pageLabel);
        if (viewModel != null)
        {
            AppLog.WriteInformation($"Navigating to page '{pageLabel}' ({viewModel.GetType().FullName}).");
            _previousPage = CurrentPage;
            _previousPageLabel = _currentPageLabel;
            CurrentPage = viewModel;
            _currentPageLabel = pageLabel;
            ActiveItem = pageLabel;
            StartPageInitialization(viewModel);
        }
    }

    /// <summary>Restores the last page known to have been usable after a dispatcher failure.</summary>
    public bool TryRecoverFromUnhandledException()
    {
        try
        {
            if (CurrentPage is AccountManagementViewModel accountManagement && accountManagement.IsCreateScreen)
            {
                accountManagement.RecoverToListAfterUnexpectedError(
                    "An unexpected error interrupted account creation. The account list has been restored.");
                AppLog.WriteInformation("Recovered from an account creation screen error to the account list.");
                return true;
            }

            if (_previousPage is not null && !string.IsNullOrWhiteSpace(_previousPageLabel))
            {
                CurrentPage = _previousPage;
                _currentPageLabel = _previousPageLabel;
                ActiveItem = _previousPageLabel;
                AppLog.WriteInformation($"Recovered navigation to previous page '{_previousPageLabel}'.");
                return true;
            }

            var fallback = NavBar.Items.FirstOrDefault(item => item.Label != _currentPageLabel);
            if (fallback is null)
            {
                return false;
            }

            var fallbackViewModel = _navigationService.GetPageViewModel(fallback.Label);
            if (fallbackViewModel is null)
            {
                return false;
            }

            CurrentPage = fallbackViewModel;
            _currentPageLabel = fallback.Label;
            ActiveItem = fallback.Label;
            AppLog.WriteInformation($"Recovered navigation to fallback page '{fallback.Label}'.");
            return true;
        }
        catch (Exception recoveryException)
        {
            AppLog.WriteError("Could not recover navigation after an unhandled UI exception.", recoveryException);
            return false;
        }
    }

    /// <summary>
    /// Runs <see cref="IAsyncInitializable.InitializeAsync"/> for the page just opened, and cancels
    /// any loading still running for the page the user navigated away from.
    /// </summary>
    // async void is intentional: this is fired from a navigation command, and an unexpected
    // exception must surface instead of being silently lost in an unobserved Task.
    private async void StartPageInitialization(object page)
    {
        _pageInitializationCancellation?.Cancel();
        _pageInitializationCancellation = null;

        if (page is not IAsyncInitializable initializablePage)
        {
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _pageInitializationCancellation = cancellation;

        try
        {
            await initializablePage.InitializeAsync(cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // The user left the page before it finished loading; nothing to report.
        }
        finally
        {
            if (ReferenceEquals(_pageInitializationCancellation, cancellation))
            {
                _pageInitializationCancellation = null;
            }
        }
    }

    /// <summary>
    /// Updates user information in the NavBar from AuthContext.
    /// </summary>
    private void UpdateUserInfo()
    {
        if (!_authContext.IsAuthenticated)
            return;

        NavBar.UserName = _authContext.FullName ?? _authContext.Username ?? "Unknown User";
        NavBar.UserRole = FormatRole(_authContext.Role ?? "Unknown Role");
        NavBar.UserInitials = GetInitials(_authContext.FullName ?? _authContext.Username ?? "Unknown");
        
        // Rebuild navigation items based on permissions
        NavBar.BuildNavigationItems(_authContext.PermissionFlags);
    }

    /// <summary>
    /// Gets the default page for the current user's role.
    /// </summary>
    private string GetDefaultPageForRole()
    {
        var flags = _authContext.PermissionFlags;
        
        // Prioritize pages based on role and permissions
        if (flags.CanManageUsers)
            return PageNames.UserManagement;
        if (flags.CanManageCustomers)
            return PageNames.CustomerManagement;
        if (flags.CanManageAccounts)
            return PageNames.AccountManagement;
        if (flags.CanViewTransactions)
            return PageNames.Transactions;
        if (flags.CanViewAudit)
            return PageNames.Audit;
        
        // Fall back to the first tab the user is allowed to see, never a page they lack permission for.
        return NavBar.Items.FirstOrDefault()?.Label ?? string.Empty;
    }

    /// <summary>
    /// Formats role name for display.
    /// </summary>
    private static string FormatRole(string role)
    {
        return role switch
        {
            "manager" => "Branch Manager",
            "officer" => "Banking Officer",
            "auditor" => "Auditor",
            _ => role
        };
    }

    /// <summary>
    /// Gets user initials from their name.
    /// </summary>
    private static string GetInitials(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "??";

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "??";

        if (parts.Length == 1)
            return parts[0].Length >= 2 
                ? parts[0].Substring(0, 2).ToUpper() 
                : parts[0].ToUpper();

        return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
    }
}
