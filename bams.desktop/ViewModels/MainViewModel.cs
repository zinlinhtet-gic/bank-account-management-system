using System.Windows.Input;
using bams.desktop.Commands;
using bams.desktop.Services;
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
    private string _activeItem = string.Empty;

    public MainViewModel(AuthContext authContext, INavigationService navigationService, NavBarViewModel navBarViewModel)
    {
        _authContext = authContext;
        _navigationService = navigationService;
        NavBar = navBarViewModel;
        
        // Wire up navigation from NavBar
        NavBar.NavigateCommand = new RelayCommand(NavigateToPage);
        
        // Initialize with user info from AuthContext
        UpdateUserInfo();
        
        // Load initial page
        NavigateToPage(GetDefaultPageForRole());
    }

    public NavBarViewModel NavBar { get; }

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
            CurrentPage = viewModel;
            ActiveItem = pageLabel;
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
            return "User Management";
        if (flags.CanManageCustomers)
            return "Customer Management";
        if (flags.CanManageAccounts)
            return "Account Management";
        if (flags.CanViewTransactions)
            return "Transactions";
        if (flags.CanViewAudit)
            return "Audit";
        
        // Fallback to first available page
        return "Customer Management";
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