using bams.desktop.Commands;
using bams.desktop.Models;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Operations;

/// <summary>
/// Filter bar shared by the Operations pages: customer/account search, a status drop-down and a date range.
/// A drop-down or date change, Enter in the search box, or Reset raises <see cref="FiltersChanged"/>;
/// the owning page then reloads from page 1.
/// </summary>
public sealed class OperationFilterViewModel<TStatus> : ViewModelBase where TStatus : struct, Enum
{
    private readonly FilterOption<TStatus> _allStatuses;
    private string _searchText = string.Empty;
    private FilterOption<TStatus> _selectedStatus;
    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private bool _isResetting;

    public OperationFilterViewModel(IReadOnlyList<FilterOption<TStatus>> statusOptions)
    {
        // The first option is the "All statuses" entry (null value).
        StatusOptions = statusOptions;
        _allStatuses = statusOptions[0];
        _selectedStatus = _allStatuses;

        SearchCommand = new RelayCommand(_ => FiltersChanged?.Invoke());
        ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
    }

    /// <summary>Raised when the shown list should be reloaded with the current filters.</summary>
    public event Action? FiltersChanged;

    /// <summary>Account number, customer number or customer name (partial match).</summary>
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public IReadOnlyList<FilterOption<TStatus>> StatusOptions { get; }

    public FilterOption<TStatus> SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value ?? _allStatuses))
            {
                RaiseFiltersChanged();
            }
        }
    }

    public DateTime? DateFrom
    {
        get => _dateFrom;
        set
        {
            if (SetProperty(ref _dateFrom, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    public DateTime? DateTo
    {
        get => _dateTo;
        set
        {
            if (SetProperty(ref _dateTo, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    public RelayCommand SearchCommand { get; }

    public RelayCommand ResetFiltersCommand { get; }

    /// <summary>The search text to send, or null when blank.</summary>
    public string? Search => string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();

    /// <summary>
    /// Returns the inclusive date range to send, or the validation code when "from" is after "to".
    /// </summary>
    public (DateOnly? From, DateOnly? To, MessageCode? Error) BuildDateRange()
    {
        if (DateFrom is not null && DateTo is not null && DateFrom.Value.Date > DateTo.Value.Date)
        {
            return (null, null, MessageCode.InvalidDateRange);
        }

        return (ToDateOnly(DateFrom), ToDateOnly(DateTo), null);
    }

    // Lets the owning page clear its own extra filters (e.g. fee type) as part of Reset.
    public event Action? Resetting;

    private static DateOnly? ToDateOnly(DateTime? value)
    {
        return value is null ? null : DateOnly.FromDateTime(value.Value);
    }

    // Clears every filter, then reloads once.
    private void ResetFilters()
    {
        _isResetting = true;
        try
        {
            SearchText = string.Empty;
            SelectedStatus = _allStatuses;
            DateFrom = null;
            DateTo = null;
            Resetting?.Invoke();
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
