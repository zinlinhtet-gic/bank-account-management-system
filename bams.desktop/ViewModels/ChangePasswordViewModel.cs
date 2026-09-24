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
                // Refresh the live requirement checklist shown under the field.
                OnPropertyChanged(nameof(MeetsMinimumLength));
                OnPropertyChanged(nameof(HasUppercase));
                OnPropertyChanged(nameof(HasLowercase));
                OnPropertyChanged(nameof(HasDigit));
                OnPropertyChanged(nameof(HasSpecialCharacter));
                OnPropertyChanged(nameof(PasswordsMatch));
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
                OnPropertyChanged(nameof(PasswordsMatch));
                ChangePasswordCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // ----- Password rules (mirror the server's IsPasswordValid; the server stays authoritative) -----

    /// <summary>Minimum number of characters in a new password.</summary>
    public const int MinimumPasswordLength = 8;

    public bool MeetsMinimumLength => NewPassword.Length >= MinimumPasswordLength;

    public bool HasUppercase => NewPassword.Any(char.IsUpper);

    public bool HasLowercase => NewPassword.Any(char.IsLower);

    public bool HasDigit => NewPassword.Any(char.IsDigit);

    public bool HasSpecialCharacter => NewPassword.Any(character => !char.IsLetterOrDigit(character));

    /// <summary>True once the confirmation is filled in and equals the new password.</summary>
    public bool PasswordsMatch => ConfirmPassword.Length > 0 && ConfirmPassword == NewPassword;

    private bool IsNewPasswordValid =>
        MeetsMinimumLength && HasUppercase && HasLowercase && HasDigit && HasSpecialCharacter;

    /// <summary>Name shown on the screen so the user knows which account is changing its password.</summary>
    public string UserDisplayName => _authContext.FullName ?? _authContext.Username ?? string.Empty;

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

    public RelayCommand ChangePasswordCommand { get; }

    private bool CanChangePassword()
    {
        return !IsBusy;
    }

    // Gets validation error message for the current password.
    private string GetPasswordValidationError()
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            return "Password is required.";
        }

        if (!MeetsMinimumLength)
        {
            return $"Password must be at least {MinimumPasswordLength} characters long.";
        }

        var errors = new List<string>();

        if (!HasUppercase)
            errors.Add("uppercase letter");
        if (!HasLowercase)
            errors.Add("lowercase letter");
        if (!HasDigit)
            errors.Add("number");
        if (!HasSpecialCharacter)
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
            if (!IsNewPasswordValid)
            {
                ErrorMessage = GetPasswordValidationError();
                return;
            }

            // A password change must actually change the password. The server also rejects the role default
            // passwords, which the client does not know.
            if (NewPassword == CurrentPassword)
            {
                ErrorMessage = MessageCatalog.GetMessage(MessageCode.NewPasswordSameAsCurrent);
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
                _authContext.UserId = permissionsResponse.UserId;

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