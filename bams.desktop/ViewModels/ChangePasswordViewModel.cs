using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using bams.desktop.Commands;
using bams.desktop.DTOs.Auth;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels;

/// <summary>
/// ViewModel for the password change screen.
/// </summary>
public sealed class ChangePasswordViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private string _currentPassword = string.Empty;
    private string _newPassword = string.Empty;
    private string _confirmPassword = string.Empty;
    private bool _isBusy;
    private string _errorMessage = string.Empty;
    private string _statusMessage = string.Empty;

    public ChangePasswordViewModel(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
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
                StatusMessage = response.Message;
                OnPasswordChangeSuccess?.Invoke();
            }
            else
            {
                ErrorMessage = response.Message;
            }
        }
        catch (NetworkException)
        {
            ErrorMessage = "Network error: Unable to connect to the server. Please check your internet connection and try again.";
        }
        catch (ApiException ex)
        {
            // Provide more specific error messages based on the exception message
            if (ex.Message.Contains("Invalid credentials") || ex.Message.Contains("current password"))
            {
                ErrorMessage = "Current password is incorrect. Please verify your password and try again.";
            }
            else if (ex.Message.Contains("Password must be at least") || ex.Message.Contains("does not meet requirements"))
            {
                ErrorMessage = GetPasswordValidationError();
            }
            else if (ex.Message.Contains("Account not found") || ex.Message.Contains("User not found"))
            {
                ErrorMessage = "Account not found. Your session may have expired. Please log in again.";
            }
            else if (ex.Message.Contains("Authentication required") || ex.Message.Contains("Unauthorized"))
            {
                ErrorMessage = "Authentication session expired. Please log in again.";
            }
            else if (ex.Message.Contains("Access denied"))
            {
                ErrorMessage = "Access denied. You do not have permission to change your password.";
            }
            else
            {
                ErrorMessage = $"Password change error: {ex.Message}";
            }
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred during password change. Please try again or contact support if the problem persists.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Event raised when password change is successful for navigation purposes
    public event Action? OnPasswordChangeSuccess;
}