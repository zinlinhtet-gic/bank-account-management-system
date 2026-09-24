using System.ComponentModel;
using System.Runtime.CompilerServices;
using bams.desktop.Constants;

namespace bams.desktop.Services;

/// <summary>
/// Authentication context for managing user authentication state and permissions.
/// Similar to Global_AMS AppContext pattern.
/// </summary>
public sealed class AuthContext : INotifyPropertyChanged
{
    private static AuthContext? _instance;
    private string? _username;
    private string? _fullName;
    private string? _role;
    private List<string> _permissions = new();
    private string? _token;
    private bool _isAuthenticated;
    private DateTime _tokenExpiry;

    public static AuthContext Instance => _instance ??= new AuthContext();

    private AuthContext()
    {
        _isAuthenticated = false;
        _permissions = new List<string>();
    }

    /// <summary>
    /// The signed-in user's id, set once permissions are loaded. Use it to recognise the user's own record
    /// (e.g. self-delete); never send it to the server as "who is acting", the server reads that from the token.
    /// </summary>
    public long? UserId { get; set; }

    public string? Username
    {
        get => _username;
        set
        {
            if (_username != value)
            {
                _username = value;
                OnPropertyChanged();
            }
        }
    }

    public string? FullName
    {
        get => _fullName;
        set
        {
            if (_fullName != value)
            {
                _fullName = value;
                OnPropertyChanged();
            }
        }
    }

    public string? Role
    {
        get => _role;
        set
        {
            if (_role != value)
            {
                _role = value;
                OnPropertyChanged();
            }
        }
    }

    public List<string> Permissions
    {
        get => _permissions;
        set
        {
            if (_permissions != value)
            {
                _permissions = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PermissionFlags));
            }
        }
    }

    public string? Token
    {
        get => _token;
        set
        {
            if (_token != value)
            {
                _token = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set
        {
            if (_isAuthenticated != value)
            {
                _isAuthenticated = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime TokenExpiry
    {
        get => _tokenExpiry;
        set
        {
            if (_tokenExpiry != value)
            {
                _tokenExpiry = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Computed permission flags for UI elements.
    /// Similar to Global_AMS PermissionFlags pattern.
    /// </summary>
    public PermissionFlags PermissionFlags => ComputePermissionFlags();

    private PermissionFlags ComputePermissionFlags()
    {
        return new PermissionFlags
        {
            CanManageAccounts = HasPermission(PermissionCodes.AccountManagement),
            CanManageCustomers = HasPermission(PermissionCodes.CustomerManagement),
            CanPerformKYC = HasPermission(PermissionCodes.CustomerKyc),
            CanManageUsers = HasPermission(PermissionCodes.UserManagement),
            CanViewTransactions = HasPermission(PermissionCodes.Transactions),
            CanViewTransactionHistory = HasPermission(PermissionCodes.TransactionHistory),
            CanAccessAccounting = HasPermission(PermissionCodes.Accounting),
            CanConfigureSystem = HasPermission(PermissionCodes.Configuration),
            CanPerformOperations = HasPermission(PermissionCodes.Operation),
            CanViewAudit = HasPermission(PermissionCodes.Audit),
            CanViewCustomerList = HasPermission(PermissionCodes.CustomerList)
        };
    }

    /// <summary>
    /// Checks if the current user has a specific permission.
    /// </summary>
    public bool HasPermission(string permission)
    {
        return _permissions.Contains(permission.ToLower());
    }

    /// <summary>
    /// Checks if the current user has any of the specified permissions.
    /// </summary>
    public bool HasAnyPermission(params string[] permissions)
    {
        return permissions.Any(p => _permissions.Contains(p.ToLower()));
    }

    /// <summary>
    /// Sets the current user session.
    /// </summary>
    public void SetSession(string username, string fullName, string role, List<string> permissions, string token, DateTime tokenExpiry)
    {
        Username = username;
        FullName = fullName;
        Role = role;
        Permissions = permissions;
        Token = token;
        TokenExpiry = tokenExpiry;
        IsAuthenticated = true;
    }

    /// <summary>
    /// Clears the current user session.
    /// </summary>
    public void ClearSession()
    {
        UserId = null;
        Username = null;
        FullName = null;
        Role = null;
        Permissions = new List<string>();
        Token = null;
        TokenExpiry = DateTime.MinValue;
        IsAuthenticated = false;
    }

    /// <summary>
    /// Checks if the current token is expired.
    /// </summary>
    public bool IsTokenExpired()
    {
        return DateTime.UtcNow >= _tokenExpiry;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Permission flags for UI element visibility and enablement.
/// Similar to Global_AMS PermissionFlags pattern.
/// </summary>
public sealed class PermissionFlags
{
    public bool CanManageAccounts { get; set; }
    public bool CanManageCustomers { get; set; }
    public bool CanPerformKYC { get; set; }
    public bool CanManageUsers { get; set; }
    public bool CanViewTransactions { get; set; }
    public bool CanViewTransactionHistory { get; set; }
    public bool CanAccessAccounting { get; set; }
    public bool CanConfigureSystem { get; set; }
    public bool CanPerformOperations { get; set; }
    public bool CanViewAudit { get; set; }
    public bool CanViewCustomerList { get; set; }
}