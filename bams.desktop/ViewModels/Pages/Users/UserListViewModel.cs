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
        try
        {
            IsLoading = true;

            var users = await _userService.GetUsersAsync(filter, cancellationToken);

            Users.Clear();
            foreach (var user in users)
            {
                Users.Add(UserDisplayModel.FromResponse(user, _authContext.UserId));
            }

            OnPropertyChanged(nameof(CountText));
        }
        finally
        {
            IsLoading = false;
        }
    }
}
