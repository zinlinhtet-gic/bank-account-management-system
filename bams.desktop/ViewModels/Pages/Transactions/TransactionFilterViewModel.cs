using bams.desktop.Commands;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Models;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// The filter bar above a transaction table: account number, type, status and date range.
/// Type, status and dates apply as soon as they change; the account number applies on Enter or the search icon.
/// Shared by the Transactions and Transaction History pages.
/// </summary>
public sealed class TransactionFilterViewModel : ViewModelBase
{
    private static readonly FilterOption<TransactionType> AllTypes = new(null, "All types");
    private static readonly FilterOption<TransactionStatus> AllStatuses = new(null, "All statuses");

    private string _accountNo = string.Empty;
    private FilterOption<TransactionType> _selectedType = AllTypes;
    private FilterOption<TransactionStatus> _selectedStatus = AllStatuses;
    private DateTime? _dateFrom;
    private DateTime? _dateTo;

    // Set while ResetFilters clears several fields, so the list reloads once instead of once per field.
    private bool _isResetting;

    public TransactionFilterViewModel()
    {
        // Only the types the server posts today; fees and interest will join when those features exist.
        TypeOptions =
        [
            AllTypes,
            .. new[]
            {
                TransactionType.CashDeposit,
                TransactionType.CashWithdrawal,
                TransactionType.InternalTransfer,
                TransactionType.InterbankTransfer,
                TransactionType.NrcTransfer,
                TransactionType.Reversal
            }.Select(type => new FilterOption<TransactionType>(type, TransactionDisplay.ToDisplayName(type)))
        ];

        StatusOptions =
        [
            AllStatuses,
            .. new[]
            {
                TransactionStatus.Pending,
                TransactionStatus.Completed,
                TransactionStatus.Failed,
                TransactionStatus.Cancelled
            }.Select(status => new FilterOption<TransactionStatus>(status, TransactionDisplay.ToDisplayName(status)))
        ];

        SearchCommand = new RelayCommand(_ => RaiseFiltersChanged());
        ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
    }

    /// <summary>
    /// Raised when the list should reload from the first page with the current filters.
    /// </summary>
    public event Action? FiltersChanged;

    public IReadOnlyList<FilterOption<TransactionType>> TypeOptions { get; }

    public IReadOnlyList<FilterOption<TransactionStatus>> StatusOptions { get; }

    public RelayCommand SearchCommand { get; }

    public RelayCommand ResetFiltersCommand { get; }

    /// <summary>Account number to show transactions for (exact match).</summary>
    public string AccountNo
    {
        get => _accountNo;
        set => SetProperty(ref _accountNo, value);
    }

    public FilterOption<TransactionType> SelectedType
    {
        get => _selectedType;
        set
        {
            // The ComboBox briefly sends null while its items are replaced; keep the current choice then.
            if (value is not null && SetProperty(ref _selectedType, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    public FilterOption<TransactionStatus> SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (value is not null && SetProperty(ref _selectedStatus, value))
            {
                RaiseFiltersChanged();
            }
        }
    }

    /// <summary>First day to include (local date).</summary>
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

    /// <summary>Last day to include (local date, inclusive).</summary>
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

    /// <summary>
    /// Shows only this status (and type, when given), e.g. every pending transfer, and reloads once.
    /// </summary>
    public void ShowOnly(TransactionType? type, TransactionStatus status)
    {
        _isResetting = true;
        try
        {
            SelectedType = TypeOptions.First(option => option.Value == type);
            SelectedStatus = StatusOptions.First(option => option.Value == status);
        }
        finally
        {
            _isResetting = false;
        }

        FiltersChanged?.Invoke();
    }

    /// <summary>
    /// Returns the filter to send to the server, or the validation code when the date range is inverted.
    /// </summary>
    public (TransactionListFilter? Filter, MessageCode? Error) BuildFilter()
    {
        if (DateFrom is not null && DateTo is not null && DateFrom.Value.Date > DateTo.Value.Date)
        {
            return (null, MessageCode.InvalidDateRange);
        }

        // "To" is inclusive on screen, so send the start of the following day as the exclusive bound.
        var filter = new TransactionListFilter(
            AccountNo,
            SelectedType.Value,
            SelectedStatus.Value,
            DateFrom is null ? null : DateTimeDisplay.StartOfLocalDay(DateFrom.Value),
            DateTo is null ? null : DateTimeDisplay.StartOfLocalDay(DateTo.Value.AddDays(1)));

        return (filter, null);
    }

    // Clears every filter, then reloads once with all transactions, newest first.
    private void ResetFilters()
    {
        _isResetting = true;
        try
        {
            AccountNo = string.Empty;
            SelectedType = AllTypes;
            SelectedStatus = AllStatuses;
            DateFrom = null;
            DateTo = null;
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
