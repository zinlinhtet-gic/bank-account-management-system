using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Users;
using bams.desktop.Models;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Users;

/// <summary>
/// The filter bar above the user table: search text, role and created-date range.
/// The role and dates apply as soon as they change; the search text applies on Enter or the search icon.
/// </summary>
public sealed class UserFilterViewModel : ViewModelBase
{
    private string _searchText = string.Empty;
    private RoleOption _selectedRole = RoleOption.AllRoles;
    private DateTime? _createdFrom;
    private DateTime? _createdTo;

    // Set while ResetFilters clears several fields, so the list reloads once instead of once per field.
    private bool _isResetting;

    public UserFilterViewModel()
    {
        RoleOptions.Add(RoleOption.AllRoles);

        SearchCommand = new RelayCommand(_ => RaiseFiltersChanged());
        ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
    }

    /// <summary>
    /// Raised when the list should reload with the current filters.
    /// </summary>
    public event Action? FiltersChanged;

    /// <summary>"All roles" followed by the assignable roles.</summary>
    public ObservableCollection<RoleOption> RoleOptions { get; } = [];

    public RelayCommand SearchCommand { get; }

    public RelayCommand ResetFiltersCommand { get; }

    /// <summary>Username, full name or email to search for.</summary>
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public RoleOption SelectedRole
    {
        get => _selectedRole;
        set
        {
            // The ComboBox briefly sends null while its items are replaced; keep the current choice then.
            if (value is not null && SetProperty(ref _selectedRole, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    /// <summary>First creation day to include (local date).</summary>
    public DateTime? CreatedFrom
    {
        get => _createdFrom;
        set
        {
            if (SetProperty(ref _createdFrom, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    /// <summary>Last creation day to include (local date, inclusive).</summary>
    public DateTime? CreatedTo
    {
        get => _createdTo;
        set
        {
            if (SetProperty(ref _createdTo, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    /// <summary>
    /// Replaces the role choices with the server's assignable roles, keeping "All roles" first.
    /// </summary>
    public void SetRoles(IEnumerable<RoleOption> roles)
    {
        var selectedCode = SelectedRole.Code;

        // Keep "All roles" (index 0) in the list: clearing it would make the ComboBox drop its selection
        // and show an empty box even though SelectedRole is still "All roles".
        while (RoleOptions.Count > 1)
        {
            RoleOptions.RemoveAt(RoleOptions.Count - 1);
        }

        foreach (var role in roles)
        {
            RoleOptions.Add(role);
        }

        // Restore the previous choice silently; this is not a filter change by the user.
        _selectedRole = RoleOptions.FirstOrDefault(role => role.Code == selectedCode) ?? RoleOption.AllRoles;
        OnPropertyChanged(nameof(SelectedRole));
    }

    /// <summary>
    /// Returns the filter to send to the server, or the validation code when the date range is inverted.
    /// </summary>
    public (UserListFilter? Filter, MessageCode? Error) BuildFilter()
    {
        if (CreatedFrom is not null && CreatedTo is not null && CreatedFrom.Value.Date > CreatedTo.Value.Date)
        {
            return (null, MessageCode.InvalidDateRange);
        }

        // "To" is inclusive on screen, so send the start of the following day as the exclusive bound.
        var filter = new UserListFilter(
            SearchText,
            SelectedRole.Code,
            CreatedFrom is null ? null : DateTimeDisplay.StartOfLocalDay(CreatedFrom.Value),
            CreatedTo is null ? null : DateTimeDisplay.StartOfLocalDay(CreatedTo.Value.AddDays(1)));

        return (filter, null);
    }

    // Clears every filter, then reloads once with the original list (all users, sorted by username).
    private void ResetFilters()
    {
        _isResetting = true;
        try
        {
            SearchText = string.Empty;
            SelectedRole = RoleOption.AllRoles;
            CreatedFrom = null;
            CreatedTo = null;
        }
        finally
        {
            _isResetting = false;
        }

        FiltersChanged?.Invoke();
    }

    private void RaiseFiltersChanged()
    {
        if (!_isResetting)
        {
            FiltersChanged?.Invoke();
        }
    }
}
