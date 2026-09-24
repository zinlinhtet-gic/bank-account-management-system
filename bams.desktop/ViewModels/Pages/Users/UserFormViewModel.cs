using System.Text.RegularExpressions;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Users;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Users;

/// <summary>
/// The create / edit user form shown in a modal. First and last name are merged into the single FullName the
/// server stores. Closes itself with <c>true</c> after a successful save; <see cref="SavedUser"/> holds the result.
/// </summary>
public sealed class UserFormViewModel : ViewModelBase, IDialogViewModel
{
    private static readonly Regex UsernameRegex = new(UserFieldRules.UsernamePattern, RegexOptions.CultureInvariant);
    private static readonly Regex EmailRegex = new(UserFieldRules.EmailPattern, RegexOptions.CultureInvariant);
    private static readonly Regex PhoneRegex = new(UserFieldRules.PhonePattern, RegexOptions.CultureInvariant);

    private readonly IUserService _userService;
    private readonly IDialogService _dialogService;
    private readonly UserResponse? _existingUser;
    private readonly bool _isEditingSelf;

    private string _firstName = string.Empty;
    private string _lastName = string.Empty;
    private string _username = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private RoleOption? _selectedRole;

    private string _firstNameError = string.Empty;
    private string _usernameError = string.Empty;
    private string _emailError = string.Empty;
    private string _phoneError = string.Empty;
    private string _roleError = string.Empty;
    private string _formError = string.Empty;
    private bool _isBusy;

    /// <summary>
    /// Creates the form. Pass <paramref name="existingUser"/> to edit that user; null creates a new one.
    /// </summary>
    public UserFormViewModel(
        IUserService userService,
        IDialogService dialogService,
        IReadOnlyList<RoleOption> roles,
        UserResponse? existingUser,
        long? currentUserId)
    {
        _userService = userService;
        _dialogService = dialogService;
        _existingUser = existingUser;
        _isEditingSelf = existingUser is not null && existingUser.Id == currentUserId;
        Roles = roles;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false), _ => CanCancel);

        if (existingUser is not null)
        {
            FillFromExistingUser(existingUser);
        }
    }

    public event Action<bool>? CloseRequested;

    // A stray click outside must not throw away what was typed.
    public bool CanCloseOnBackdropClick => false;

    public bool CanCancel => !IsBusy;

    public bool IsEditMode => _existingUser is not null;

    public string Title => IsEditMode ? "Edit user" : "Create new user";

    public string Subtitle => IsEditMode
        ? $"Update {_existingUser!.Username}'s details and role."
        : "Add a staff member and choose what they can access.";

    public string SaveText => IsEditMode ? "Save changes" : "Create user";

    public IReadOnlyList<RoleOption> Roles { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>The saved user, set just before the dialog closes with success.</summary>
    public UserResponse? SavedUser { get; private set; }

    /// <summary>True when the signed-in user changed their own role; the page then signs them out.</summary>
    public bool ChangedOwnRole { get; private set; }

    public string FirstName
    {
        get => _firstName;
        set
        {
            if (SetProperty(ref _firstName, value))
            {
                FirstNameError = string.Empty;
            }
        }
    }

    public string LastName
    {
        get => _lastName;
        set => SetProperty(ref _lastName, value);
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                UsernameError = string.Empty;
            }
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
            {
                EmailError = string.Empty;
            }
        }
    }

    public string Phone
    {
        get => _phone;
        set
        {
            if (SetProperty(ref _phone, value))
            {
                PhoneError = string.Empty;
            }
        }
    }

    public RoleOption? SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetProperty(ref _selectedRole, value))
            {
                RoleError = string.Empty;
                OnPropertyChanged(nameof(DefaultPasswordNotice));
                OnPropertyChanged(nameof(HasDefaultPasswordNotice));
            }
        }
    }

    /// <summary>Create mode only: tells the manager which password to pass on to the new user.</summary>
    public string DefaultPasswordNotice => SelectedRole is null
        ? string.Empty
        : $"The new user signs in with the {SelectedRole.DisplayName} default password {SelectedRole.DefaultPassword} and must choose a new one at first sign-in.";

    public bool HasDefaultPasswordNotice => !IsEditMode && SelectedRole is not null;

    /// <summary>Edit-self only: warns that changing one's own role ends the session.</summary>
    public bool ShowsOwnRoleWarning => _isEditingSelf;

    public string FirstNameError
    {
        get => _firstNameError;
        private set
        {
            if (SetProperty(ref _firstNameError, value))
            {
                OnPropertyChanged(nameof(HasFirstNameError));
            }
        }
    }

    public bool HasFirstNameError => !string.IsNullOrEmpty(FirstNameError);

    public string UsernameError
    {
        get => _usernameError;
        private set
        {
            if (SetProperty(ref _usernameError, value))
            {
                OnPropertyChanged(nameof(HasUsernameError));
            }
        }
    }

    public bool HasUsernameError => !string.IsNullOrEmpty(UsernameError);

    public string EmailError
    {
        get => _emailError;
        private set
        {
            if (SetProperty(ref _emailError, value))
            {
                OnPropertyChanged(nameof(HasEmailError));
            }
        }
    }

    public bool HasEmailError => !string.IsNullOrEmpty(EmailError);

    public string PhoneError
    {
        get => _phoneError;
        private set
        {
            if (SetProperty(ref _phoneError, value))
            {
                OnPropertyChanged(nameof(HasPhoneError));
            }
        }
    }

    public bool HasPhoneError => !string.IsNullOrEmpty(PhoneError);

    public string RoleError
    {
        get => _roleError;
        private set
        {
            if (SetProperty(ref _roleError, value))
            {
                OnPropertyChanged(nameof(HasRoleError));
            }
        }
    }

    public bool HasRoleError => !string.IsNullOrEmpty(RoleError);

    /// <summary>Error that belongs to no single field (network, last-manager rule...).</summary>
    public string FormError
    {
        get => _formError;
        private set
        {
            if (SetProperty(ref _formError, value))
            {
                OnPropertyChanged(nameof(HasFormError));
            }
        }
    }

    public bool HasFormError => !string.IsNullOrEmpty(FormError);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                OnPropertyChanged(nameof(CanCancel));
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Merges first and last name into the stored full name, e.g. "Aung" + "Kyaw Moe" → "Aung Kyaw Moe".
    /// </summary>
    public static string MergeFullName(string firstName, string lastName)
    {
        return string.Join(' ', new[] { firstName.Trim(), lastName.Trim() }.Where(part => part.Length > 0));
    }

    // Edit mode: the first word becomes the first name and the rest the last name.
    private void FillFromExistingUser(UserResponse user)
    {
        var nameParts = user.FullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        _firstName = nameParts.ElementAtOrDefault(0) ?? string.Empty;
        _lastName = nameParts.ElementAtOrDefault(1) ?? string.Empty;
        _username = user.Username;
        _email = user.Email;
        _phone = user.Phone ?? string.Empty;
        _selectedRole = Roles.FirstOrDefault(role => role.Code == user.Role);
    }

    // Validates locally, confirms a change to one's own role, then creates or updates the user.
    private async Task SaveAsync()
    {
        FormError = string.Empty;

        if (!ValidateFields())
        {
            return;
        }

        var fullName = MergeFullName(FirstName, LastName);
        var phone = string.IsNullOrWhiteSpace(Phone) ? null : Phone.Trim();
        var roleCode = SelectedRole!.Code!;
        var changesOwnRole = _isEditingSelf && _existingUser!.Role != roleCode;

        // Changing one's own role takes effect at once on the server, so the current session has to end.
        if (changesOwnRole && !ConfirmOwnRoleChange())
        {
            return;
        }

        try
        {
            IsBusy = true;

            SavedUser = IsEditMode
                ? await _userService.UpdateUserAsync(
                    _existingUser!.Id,
                    new UpdateUserRequest(fullName, Username.Trim(), Email.Trim(), phone, roleCode),
                    CancellationToken.None)
                : await _userService.CreateUserAsync(
                    new CreateUserRequest(fullName, Username.Trim(), Email.Trim(), phone, roleCode),
                    CancellationToken.None);

            ChangedOwnRole = changesOwnRole;
        }
        catch (AppException exception)
        {
            ShowServerError(exception);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        CloseRequested?.Invoke(true);
    }

    // Shows every field error at once so the user can fix them in one go; returns true when all are valid.
    private bool ValidateFields()
    {
        var fullName = MergeFullName(FirstName, LastName);

        FirstNameError = FirstName.Trim().Length == 0
            ? "Please enter the first name."
            : fullName.Length > UserFieldRules.FullNameMaximumLength
                ? MessageCatalog.GetMessage(MessageCode.FieldTooLong)
                : string.Empty;

        UsernameError = Username.Trim().Length == 0
            ? "Please enter a username."
            : UsernameRegex.IsMatch(Username.Trim())
                ? string.Empty
                : MessageCatalog.GetMessage(MessageCode.InvalidUsernameFormat);

        EmailError = Email.Trim().Length == 0
            ? "Please enter an email address."
            : Email.Trim().Length > UserFieldRules.EmailMaximumLength
                ? MessageCatalog.GetMessage(MessageCode.FieldTooLong)
                : EmailRegex.IsMatch(Email.Trim())
                    ? string.Empty
                    : MessageCatalog.GetMessage(MessageCode.InvalidEmailFormat);

        PhoneError = Phone.Trim().Length == 0 || PhoneRegex.IsMatch(Phone.Trim())
            ? string.Empty
            : MessageCatalog.GetMessage(MessageCode.InvalidPhoneFormat);

        RoleError = SelectedRole?.Code is null
            ? MessageCatalog.GetMessage(MessageCode.InvalidRole)
            : string.Empty;

        return !HasFirstNameError && !HasUsernameError && !HasEmailError && !HasPhoneError && !HasRoleError;
    }

    // Asks before a manager changes their own role, because they will be signed out and lose this page.
    private bool ConfirmOwnRoleChange()
    {
        return _dialogService.Confirm(new ConfirmDialogOptions(
            Title: "Change your own role?",
            Message: $"You will become {SelectedRole!.DisplayName} and be signed out right away. Sign in again to continue with your new access.",
            ConfirmText: "Change and sign out",
            IsDestructive: true));
    }

    // Puts a server rejection next to the field it is about; anything else goes to the banner.
    private void ShowServerError(AppException exception)
    {
        switch (exception.Code)
        {
            case MessageCode.UsernameAlreadyExists:
            case MessageCode.InvalidUsernameFormat:
                UsernameError = exception.Message;
                break;
            case MessageCode.EmailAlreadyExists:
            case MessageCode.InvalidEmailFormat:
                EmailError = exception.Message;
                break;
            case MessageCode.InvalidPhoneFormat:
                PhoneError = exception.Message;
                break;
            case MessageCode.InvalidRole:
                RoleError = exception.Message;
                break;
            default:
                FormError = exception.Message;
                break;
        }
    }
}
