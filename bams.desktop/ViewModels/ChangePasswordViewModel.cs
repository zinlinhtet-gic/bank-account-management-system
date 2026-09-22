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

    public RelayCommand ChangePasswordCommand { get; }

    private bool CanChangePassword()
    {
        return !IsBusy &&
               !string.IsNullOrWhiteSpace(CurrentPassword) &&
               !string.IsNullOrWhiteSpace(NewPassword) &&
               !string.IsNullOrWhiteSpace(ConfirmPassword) &&
               NewPassword == ConfirmPassword;
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

            // Validate passwords match
            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "New password and confirmation do not match.";
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
            ErrorMessage = "Network error: Unable to connect to the server. Please check your connection.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = $"Password change error: {ex.Message}";
        }
        catch (Exception)
        {
            ErrorMessage = "An unexpected error occurred during password change.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Event raised when password change is successful for navigation purposes
    public event Action? OnPasswordChangeSuccess;
}