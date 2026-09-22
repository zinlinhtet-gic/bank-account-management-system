using System.ComponentModel;
using System.Runtime.CompilerServices;
using bams.desktop.Services;

namespace bams.desktop.ViewModels;

/// <summary>
/// Simple ViewModel for the main application after login.
/// Demonstrates permission-based UI control.
/// </summary>
public sealed class MainAppViewModel : INotifyPropertyChanged
{
    private readonly AuthContext _authContext;

    public MainAppViewModel(AuthContext authContext)
    {
        _authContext = authContext;
        _authContext.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AuthContext.PermissionFlags))
            {
                OnPropertyChanged(nameof(CanViewAccounts));
                OnPropertyChanged(nameof(CanManageCustomers));
                OnPropertyChanged(nameof(CanViewTransactions));
                OnPropertyChanged(nameof(CanAccessAccounting));
                OnPropertyChanged(nameof(CanManageUsers));
            }
        };
    }

    public string WelcomeMessage => $"Welcome, {_authContext.FullName}!";
    public string UserRole => $"Role: {_authContext.Role}";
    public string UserPermissions => $"Permissions: {string.Join(", ", _authContext.Permissions)}";

    // Permission-based properties for UI control
    public bool CanViewAccounts => _authContext.PermissionFlags.CanViewAccounts;
    public bool CanManageCustomers => _authContext.PermissionFlags.CanManageCustomers;
    public bool CanViewTransactions => _authContext.PermissionFlags.CanViewTransactions;
    public bool CanAccessAccounting => _authContext.PermissionFlags.CanAccessAccounting;
    public bool CanManageUsers => _authContext.PermissionFlags.CanManageUsers;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}