using System.Collections.ObjectModel;
using bams.desktop.DTOs.Users;
using bams.desktop.Models;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Users;

/// <summary>
/// The user table: its rows, loading state and empty state. Errors are passed back to the page, which shows them.
/// </summary>
public sealed class UserListViewModel : ViewModelBase
{
    private readonly IUserService _userService;
    private readonly AuthContext _authContext;

    private bool _isLoading;

    // Incremented by every full load so only the newest one may clear IsLoading, and a background presence
    // refresh started before a newer load never overwrites that load's rows.
    private int _loadVersion;

    // The filter of the last load that succeeded; presence refreshes re-read exactly what the table shows,
    // not search text the user has typed but not submitted yet.
    private UserListFilter? _appliedFilter;

    public UserListViewModel(IUserService userService, AuthContext authContext)
    {
        _userService = userService;
        _authContext = authContext;
    }

    public ObservableCollection<UserDisplayModel> Users { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    /// <summary>True when a finished load returned no rows (shows the "no users" message).</summary>
    public bool IsEmpty => !IsLoading && Users.Count == 0;

    /// <summary>Counter shown next to the table title, e.g. "12 users".</summary>
    public string CountText => Users.Count == 1 ? "1 user" : $"{Users.Count} users";

    /// <summary>
    /// Loads the users matching the filter and replaces the rows only after the call succeeds.
    /// </summary>
    /// <exception cref="Exceptions.AppException">Server or network failure; the page shows the message.</exception>
    public async Task LoadAsync(UserListFilter filter, CancellationToken cancellationToken)
    {
        var version = ++_loadVersion;

        try
        {
            IsLoading = true;

            var users = await _userService.GetUsersAsync(filter, cancellationToken);

            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(UserDisplayModel.FromResponse(user, _authContext.UserId));
            }

            _appliedFilter = filter;
            OnPropertyChanged(nameof(CountText));
        }
        finally
        {
            // A superseded load must not hide the spinner of the newer load that is still running.
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Quietly re-reads the list and updates who is online in place, without the loading state, so the table
    /// does not flicker or lose its selection. If users were added or removed meanwhile, the rows are replaced.
    /// </summary>
    /// <exception cref="Exceptions.AppException">Server or network failure; the page ignores it for background refreshes.</exception>
    public async Task RefreshPresenceAsync(CancellationToken cancellationToken)
    {
        // A full load is running (or none has succeeded yet); a load brings fresh presence itself.
        if (IsLoading || _appliedFilter is null)
        {
            return;
        }

        var version = _loadVersion;
        var users = await _userService.GetUsersAsync(_appliedFilter, cancellationToken);

        // A full load started while this refresh was in flight; its result is newer, so drop this one.
        if (version != _loadVersion || IsLoading)
        {
            return;
        }

        var isSameRowSet = users.Count == Users.Count
            && users.Select(user => user.Id).SequenceEqual(Users.Select(row => row.Id));

        if (isSameRowSet)
        {
            for (var index = 0; index < users.Count; index++)
            {
                Users[index].UpdatePresence(users[index].IsOnline, users[index].LastSeenAt);
            }

            return;
        }

        Users.Clear();
        foreach (var user in users)
        {
            Users.Add(UserDisplayModel.FromResponse(user, _authContext.UserId));
        }

        OnPropertyChanged(nameof(CountText));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
