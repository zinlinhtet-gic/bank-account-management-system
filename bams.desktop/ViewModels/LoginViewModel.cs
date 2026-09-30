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

    // Authenticates the user through the service layer and sets up the auth context. The next screen is opened only
    // after the login attempt has fully finished, so a failure while building that screen is not reported as a
    // failed login (and is left to the application's unhandled-exception handling).
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        // Set once the server accepted the credentials; a later failure must undo the half-signed-in state.
        var isTokenSet = false;
        Action? openNextScreen = null;

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
            isTokenSet = true;

            // Check if password change is required
            RequiresPasswordChange = response.RequiresPasswordChange;

            if (RequiresPasswordChange)
            {
                // Preserve the authenticated identity while the password change screen is shown.
                // Permissions are loaded after the password is changed.
                _authContext.SetSession(
                    response.Username,
                    response.FullName,
                    response.Role,
                    new List<string>(),
                    response.Token,
                    response.Expiration);

                AppLog.WriteInformation("Login succeeded; password change is required before continuing.");
                StatusMessage = "You must change your password before continuing.";
                openNextScreen = OnPasswordChangeRequired;
            }
            else
            {
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
                _authContext.UserId = permissionsResponse.UserId;

                AppLog.WriteInformation("Login succeeded and user permissions were loaded.");
                StatusMessage = $"Welcome, {response.FullName}! Logged in as {response.Role}";

                // Navigate to main application once the attempt has finished (below).
                openNextScreen = OnLoginSuccess;
            }
        }
        catch (AppException exception)
        {
            // Server and network failures already carry a user-facing message for their MessageCode
            // (e.g. InvalidCredentials, UserAccountDisabled, NetworkUnavailable).
            AppLog.WriteError("Login failed because of an application or service error.", exception);
            ClearSignedInStateIfSet(isTokenSet);
            ErrorMessage = exception.Message;
        }
        catch (Exception exception)
        {
            AppLog.WriteError("Login failed because of an unexpected error.", exception);
            ClearSignedInStateIfSet(isTokenSet);
            ErrorMessage = MessageCatalog.GetMessage(MessageCode.ClientError);
        }
        finally
        {
            IsBusy = false;
        }

        openNextScreen?.Invoke();
    }

    // A step after the token was accepted failed (e.g. loading permissions), so the user is not signed in:
    // drop the bearer token and any session state so no later request runs as this user.
    private void ClearSignedInStateIfSet(bool isTokenSet)
    {
        if (!isTokenSet)
        {
            return;
        }

        _authenticationService.ClearAuthToken();
        _authContext.ClearSession();
    }

    // Event raised when login is successful for navigation purposes
    public event Action? OnLoginSuccess;

    // Event raised when password change is required
    public event Action? OnPasswordChangeRequired;
}
