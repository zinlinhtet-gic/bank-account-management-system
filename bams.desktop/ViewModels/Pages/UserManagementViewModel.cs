using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Users;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages.Users;

namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// ViewModel for the User Management page (managers only): list, filter, view, create, edit,
/// reset password and soft-delete staff users. Coordinates the <see cref="Filter"/> and <see cref="List"/>
/// components and opens the form / detail dialogs.
/// </summary>
public sealed class UserManagementViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IUserService _userService;
    private readonly IDialogService _dialogService;
    private readonly ISessionService _sessionService;
    private readonly AuthContext _authContext;

    private IReadOnlyList<RoleOption> _roles = [];
    private CancellationTokenSource? _loadCancellation;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    public UserManagementViewModel(
        IUserService userService,
        IDialogService dialogService,
        ISessionService sessionService,
        AuthContext authContext,
        UserFilterViewModel filter,
        UserListViewModel list)
    {
        // Constructors only store dependencies and create commands. No server calls here.
        _userService = userService;
        _dialogService = dialogService;
        _sessionService = sessionService;
        _authContext = authContext;
        Filter = filter;
        List = list;

        // The parent coordinates its components: a filter change reloads the list.
        Filter.FiltersChanged += OnFiltersChanged;

        RefreshCommand = new AsyncRelayCommand(() => ReloadUsersAsync(CancellationToken.None));
        CreateUserCommand = new AsyncRelayCommand(CreateUserAsync);
        ShowUserDetailsCommand = new AsyncRelayCommand(ShowUserDetailsAsync);
        EditUserCommand = new AsyncRelayCommand(EditUserAsync);
        ResetPasswordCommand = new AsyncRelayCommand(ResetPasswordAsync);
        DeleteUserCommand = new AsyncRelayCommand(DeleteUserAsync);
    }

    // Kept for the placeholder view; the header already shows the page name.
    public string PageTitle => "User Management";
    public string PageDescription => "Manage system users, roles, and permissions";

    public UserFilterViewModel Filter { get; }

    public UserListViewModel List { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand CreateUserCommand { get; }

    /// <summary>Row click: parameter is the clicked <see cref="UserDisplayModel"/>.</summary>
    public AsyncRelayCommand ShowUserDetailsCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="UserDisplayModel"/>.</summary>
    public AsyncRelayCommand EditUserCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="UserDisplayModel"/>.</summary>
    public AsyncRelayCommand ResetPasswordCommand { get; }

    /// <summary>Row action: parameter is the row's <see cref="UserDisplayModel"/>.</summary>
    public AsyncRelayCommand DeleteUserCommand { get; }

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

    public string SuccessMessage
    {
        get => _successMessage;
        private set
        {
            if (SetProperty(ref _successMessage, value))
            {
                OnPropertyChanged(nameof(HasSuccess));
            }
        }
    }

    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    // Called by MainViewModel every time the user opens this page.
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await LoadRolesAsync(cancellationToken);
        await ReloadUsersAsync(cancellationToken);

        // Keep the Online / Offline column current while the page is open. The token is cancelled when the
        // user navigates away, which ends the loop (MainViewModel treats that cancellation as normal).
        await RefreshPresencePeriodicallyAsync(cancellationToken);
    }

    // Refreshes who is online every interval. Failures are ignored here: a background refresh should never
    // replace the page's banners, and the next user action or refresh reports real problems.
    private async Task RefreshPresencePeriodicallyAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(PresenceConstants.UserListRefreshInterval);

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var (filter, error) = Filter.BuildFilter();
            if (error is not null)
            {
                continue;
            }

            try
            {
                await List.RefreshPresenceAsync(filter!, cancellationToken);
            }
            catch (AppException)
            {
            }
        }
    }

    // async void is intentional: an event handler; ReloadUsersAsync catches every expected failure itself.
    private async void OnFiltersChanged()
    {
        await ReloadUsersAsync(CancellationToken.None);
    }

    // Loads the assignable roles once per page visit for the form and the role filter.
    private async Task LoadRolesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var roles = await _userService.GetAssignableRolesAsync(cancellationToken);

            _roles = roles.Select(RoleOption.FromResponse).ToList();
            Filter.SetRoles(_roles);
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    // Reloads the table with the current filters. A newer reload cancels one still running,
    // so quickly changing filters never shows an older result last.
    private async Task ReloadUsersAsync(CancellationToken cancellationToken)
    {
        var (filter, error) = Filter.BuildFilter();
        if (error is not null)
        {
            ErrorMessage = MessageCatalog.GetMessage(error.Value);
            return;
        }

        _loadCancellation?.Cancel();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loadCancellation = cancellation;

        try
        {
            ErrorMessage = string.Empty;
            await List.LoadAsync(filter!, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Replaced by a newer reload, or the user left the page.
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
            }
        }
    }

    // Opens the empty form; after a successful save, reloads the list and confirms.
    private async Task CreateUserAsync()
    {
        ClearMessages();

        // The role picker needs the roles; retry once if the first load failed.
        if (_roles.Count == 0)
        {
            await LoadRolesAsync(CancellationToken.None);
            if (_roles.Count == 0)
            {
                return;
            }
        }

        var form = new UserFormViewModel(_userService, _dialogService, _roles, existingUser: null, _authContext.UserId);
        if (!_dialogService.ShowDialog(form))
        {
            return;
        }

        await ReloadUsersAsync(CancellationToken.None);
        SuccessMessage = $"{MessageCatalog.GetMessage(MessageCode.UserCreatedSuccessfully)} {form.SavedUser!.Username} can now sign in.";
    }

    // Loads the full record and shows it; "Edit" on the card continues straight into the edit form.
    private async Task ShowUserDetailsAsync(object? parameter)
    {
        if (parameter is not UserDisplayModel row)
        {
            return;
        }

        ClearMessages();

        var user = await GetUserDetailAsync(row.Id);
        if (user is null)
        {
            return;
        }

        // The card shows fresh presence; update the row too so the table never contradicts it.
        row.UpdatePresence(user.IsOnline, user.LastSeenAt);

        var details = new UserDetailsViewModel(user, row.IsCurrentUser);
        if (_dialogService.ShowDialog(details))
        {
            await OpenEditFormAsync(user);
        }
    }

    // Loads the latest record (not the possibly stale row) and opens the form filled with it.
    private async Task EditUserAsync(object? parameter)
    {
        if (parameter is not UserDisplayModel row)
        {
            return;
        }

        ClearMessages();

        var user = await GetUserDetailAsync(row.Id);
        if (user is not null)
        {
            row.UpdatePresence(user.IsOnline, user.LastSeenAt);
            await OpenEditFormAsync(user);
        }
    }

    // Shows the edit form; after a save, reloads the list, or signs out if the manager changed their own role.
    private async Task OpenEditFormAsync(UserResponse user)
    {
        var form = new UserFormViewModel(_userService, _dialogService, _roles, user, _authContext.UserId);
        if (!_dialogService.ShowDialog(form))
        {
            return;
        }

        if (form.ChangedOwnRole)
        {
            await _sessionService.EndSessionAsync();
            return;
        }

        await ReloadUsersAsync(CancellationToken.None);
        SuccessMessage = MessageCatalog.GetMessage(MessageCode.UserUpdatedSuccessfully);
    }

    // Confirms with the role's default password shown, then resets it.
    private async Task ResetPasswordAsync(object? parameter)
    {
        if (parameter is not UserDisplayModel row)
        {
            return;
        }

        ClearMessages();

        var defaultPassword = _roles.FirstOrDefault(role => role.Code == row.RoleCode)?.DefaultPassword;
        var passwordText = string.IsNullOrEmpty(defaultPassword)
            ? $"the {row.RoleName} default password"
            : $"the {row.RoleName} default: {defaultPassword}";

        var confirmed = _dialogService.Confirm(new ConfirmDialogOptions(
            Title: $"Reset password for {row.FullName}?",
            Message: $"Their password will be set to {passwordText}. They must choose a new password the next time they sign in.",
            ConfirmText: "Yes, reset",
            IconKey: "Icon.Key"));

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _userService.ResetPasswordAsync(row.Id, CancellationToken.None);
            SuccessMessage = $"Password for {row.Username} was reset to {passwordText}.";
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    // Confirms, then soft-deletes. Deleting one's own account signs out immediately.
    private async Task DeleteUserAsync(object? parameter)
    {
        if (parameter is not UserDisplayModel row)
        {
            return;
        }

        ClearMessages();

        var message = row.IsCurrentUser
            ? "You are deleting your own account. You will be signed out right away and will not be able to sign in again."
            : $"{row.Username} will no longer be able to sign in. The account is kept in the records but removed from this list.";

        var confirmed = _dialogService.Confirm(new ConfirmDialogOptions(
            Title: row.IsCurrentUser ? "Delete your own account?" : $"Delete {row.FullName}?",
            Message: message,
            ConfirmText: "Delete user",
            IsDestructive: true,
            IconKey: "Icon.Trash"));

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _userService.DeleteUserAsync(row.Id, CancellationToken.None);
        }
        catch (AppException exception)
        {
            // e.g. LastManagerCannotBeRemoved: the server explains why.
            ErrorMessage = exception.Message;
            return;
        }

        // The server now refuses this session, so leave instead of showing errors.
        if (row.IsCurrentUser)
        {
            await _sessionService.EndSessionAsync();
            return;
        }

        await ReloadUsersAsync(CancellationToken.None);
        SuccessMessage = $"{row.Username} was deleted.";
    }

    // Loads one user; on failure shows the error and returns null. A user deleted meanwhile refreshes the list.
    private async Task<UserResponse?> GetUserDetailAsync(long userId)
    {
        try
        {
            return await _userService.GetUserByIdAsync(userId, CancellationToken.None);
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;

            if (exception.Code == MessageCode.UserNotFound)
            {
                await ReloadUsersAsync(CancellationToken.None);
                ErrorMessage = exception.Message;
            }

            return null;
        }
    }

    // A new action replaces the previous outcome banner.
    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
    }
}
