using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using bams.desktop.Commands;
using bams.desktop.DTOs.Auth;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels;

/// <summary>
/// ViewModel for the password change screen.
/// </summary>
public sealed class ChangePasswordViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly AuthContext _authContext;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;

    public ChangePasswordViewModel(IAuthenticationService authenticationService, AuthContext authContext)
    {
        _authenticationService = authenticationService;
        _authContext = authContext;
        ChangePasswordCommand = new RelayCommand(
            async _ => await ChangePasswordAsync(CancellationToken.None),
            _ => CanChangePassword());
    }

    public string CurrentPassword
    {
        get => _currentPassword;
        set
        {
            if (SetProperty(ref _currentPassword, value))
            {
                ChangePasswordCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewPassword
    {
        get => _newPassword;
        set
        {
            if (SetProperty(ref _newPassword, value))
            {
                ChangePasswordCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
            {
                ChangePasswordCommand.RaiseCanExecuteChanged();
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
                ChangePasswordCommand.RaiseCanExecuteChanged();
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

    public string PasswordRequirements => "Password must be at least 8 characters long and include: 1 uppercase letter (A-Z), 1 lowercase letter (a-z), 1 number (0-9), and 1 special character (!@#$%^&*).";

    public RelayCommand ChangePasswordCommand { get; }

    private bool CanChangePassword()
    {
        return !IsBusy;
    }

    // Validates password complexity requirements.
    private bool IsPasswordValid(string password)
    {
        if (password.Length < 8)
        {
            return false;
        }

        bool hasUpper = password.Any(char.IsUpper);
        bool hasLower = password.Any(char.IsLower);
        bool hasDigit = password.Any(char.IsDigit);
        bool hasSpecial = password.Any(c => !char.IsLetterOrDigit(c));

        return hasUpper && hasLower && hasDigit && hasSpecial;
    }

    // Gets validation error message for the current password.
    private string GetPasswordValidationError()
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            return "Password is required.";
        }

        if (NewPassword.Length < 8)
        {
            return "Password must be at least 8 characters long.";
        }

        bool hasUpper = NewPassword.Any(char.IsUpper);
        bool hasLower = NewPassword.Any(char.IsLower);
        bool hasDigit = NewPassword.Any(char.IsDigit);
        bool hasSpecial = NewPassword.Any(c => !char.IsLetterOrDigit(c));

        var errors = new List<string>();

        if (!hasUpper)
            errors.Add("uppercase letter");
        if (!hasLower)
            errors.Add("lowercase letter");
        if (!hasDigit)
            errors.Add("number");
        if (!hasSpecial)
            errors.Add("special character");

        if (errors.Count > 0)
        {
            return $"Password must contain at least: {string.Join(", ", errors)}.";
        }

        return "Password is invalid.";
    }

    // Changes the user's password through the service layer.
    private async Task ChangePasswordAsync(CancellationToken cancellationToken)
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

            // Validate current password
            if (string.IsNullOrWhiteSpace(CurrentPassword))
            {
                ErrorMessage = "Please enter your current password.";
                return;
            }

            // Validate new password
            if (string.IsNullOrWhiteSpace(NewPassword))
            {
                ErrorMessage = "Please enter a new password.";
                return;
            }

            // Validate confirm password
            if (string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Please confirm your new password.";
                return;
            }

            // Validate passwords match
            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "New password and confirmation do not match.";
                return;
            }

            // Validate password complexity
            if (!IsPasswordValid(NewPassword))
            {
                ErrorMessage = GetPasswordValidationError();
                return;
            }

            var request = new ChangePasswordRequest(CurrentPassword, NewPassword);
            var response = await _authenticationService.ChangePasswordAsync(request, cancellationToken);

            if (response.Success)
            {
                var permissionsResponse = await _authenticationService.GetPermissionsAsync(cancellationToken);

                _authContext.SetSession(
                    _authContext.Username ?? string.Empty,
                    _authContext.FullName ?? string.Empty,
                    _authContext.Role ?? string.Empty,
                    permissionsResponse.Permissions.ToList(),
                    _authContext.Token ?? string.Empty,
                    _authContext.TokenExpiry);

                StatusMessage = response.Message;
                OnPasswordChangeSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = response.Message;
            }
        }
        catch (AppException exception)
        {
            ErrorMessage = GetChangePasswordErrorMessage(exception);
        }
        catch (Exception)
        {
            ErrorMessage = MessageCatalog.GetMessage(MessageCode.ClientError);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Chooses the message for a failed password change based on the stable MessageCode.
    private string GetChangePasswordErrorMessage(AppException exception)
    {
        return exception.Code switch
        {
            // On this screen the server's InvalidCredentials means the current password was wrong.
            MessageCode.InvalidCredentials => MessageCatalog.GetMessage(MessageCode.CurrentPasswordIncorrect),

            // Show which specific rules the new password is missing.
            MessageCode.PasswordDoesNotMeetRequirements => GetPasswordValidationError(),

            _ => exception.Message
        };
    }

    // Event raised when password change is successful for navigation purposes
    public event Action? OnPasswordChangeSuccess;
}