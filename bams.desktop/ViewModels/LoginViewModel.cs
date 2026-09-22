using System.Windows.Input;
using bams.desktop.Commands;
using bams.desktop.DTOs.Auth;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels;

/// <summary>
/// ViewModel for the login screen.
/// </summary>
public sealed class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly AuthContext _authContext;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;
    private bool _requiresPasswordChange;

    public LoginViewModel(IAuthenticationService authenticationService, AuthContext authContext)
    {
        _authenticationService = authenticationService;
        _authContext = authContext;
        LoginCommand = new RelayCommand(
            async _ => await LoginAsync(CancellationToken.None),
            _ => CanLogin());
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                LoginCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusMessage);

    public bool RequiresPasswordChange
    {
        get => _requiresPasswordChange;
        private set
        {
            if (SetProperty(ref _requiresPasswordChange, value))
            {
                OnPropertyChanged(nameof(DoesNotRequirePasswordChange));
            }
        }
    }

    public bool DoesNotRequirePasswordChange => !RequiresPasswordChange;

    public RelayCommand LoginCommand { get; }

    private bool CanLogin()
    {
        return !IsBusy;
    }

    // Authenticates the user through the service layer and sets up the auth context.
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;

            // Validate input
            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "Please enter your username.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter your password.";
                return;
            }

            var request = new LoginRequest(Username, Password);
            var response = await _authenticationService.LoginAsync(request, cancellationToken);

            // Set auth token for subsequent requests
            _authenticationService.SetAuthToken(response.Token);

            // Check if password change is required
            RequiresPasswordChange = response.RequiresPasswordChange;

            if (RequiresPasswordChange)
            {
                StatusMessage = "You must change your password before continuing.";
                OnPasswordChangeRequired?.Invoke();
                return;
            }

            // Fetch user permissions
            var permissionsResponse = await _authenticationService.GetPermissionsAsync(cancellationToken);

            // Set up the auth context with user session
            _authContext.SetSession(
                response.Username,
                response.FullName,
                response.Role,
                permissionsResponse.Permissions.ToList(),
                response.Token,
                response.Expiration);

            StatusMessage = $"Welcome, {response.FullName}! Logged in as {response.Role}";

            // Navigate to main application
            OnLoginSuccess?.Invoke();
        }
        catch (NetworkException)
        {
            ErrorMessage = "Network error: Unable to connect to the server. Please check your internet connection and try again.";
        }
        catch (ApiException ex)
        {
            // Provide more specific error messages based on the exception message
            if (ex.Message.Contains("Invalid credentials") || ex.Message.Contains("Invalid username or password"))
            {
                ErrorMessage = "Invalid username or password. Please check your credentials and try again.";
            }
            else if (ex.Message.Contains("Account not found") || ex.Message.Contains("User not found"))
            {
                ErrorMessage = "Account not found. Please check your username or contact your administrator.";
            }
            else if (ex.Message.Contains("Account locked") || ex.Message.Contains("Account disabled"))
            {
                ErrorMessage = "Your account has been locked or disabled. Please contact your administrator.";
            }
            else if (ex.Message.Contains("Authentication required"))
            {
                ErrorMessage = "Authentication session expired. Please log in again.";
            }
            else if (ex.Message.Contains("Access denied") || ex.Message.Contains("Unauthorized"))
            {
                ErrorMessage = "Access denied. You do not have permission to access this system.";
            }
            else
            {
                ErrorMessage = $"Authentication error: {ex.Message}";
            }
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred during login. Please try again or contact support if the problem persists.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Event raised when login is successful for navigation purposes
    public event Action? OnLoginSuccess;

    // Event raised when password change is required
    public event Action? OnPasswordChangeRequired;
}