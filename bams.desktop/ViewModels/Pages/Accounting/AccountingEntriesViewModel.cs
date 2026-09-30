using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>
/// Loads, filters and exposes accounting transaction entries.
/// </summary>
public sealed class AccountingEntriesViewModel :
    ViewModelBase,
    IAsyncInitializable
{
    private readonly IAccountingService _accountingService;

    private readonly List<AccountingEntryResponse> _allEntries = [];

    private bool _isLoading;
    private bool _isRefreshing;
    private string? _errorMessage;

    private DateTime? _fromDate;
    private DateTime? _toDate;

    private FilterOption<long>? _selectedGlAccount;
    private FilterOption<EntryType>? _selectedEntryType;

    /// <summary>
    /// Represents one selectable filter option.
    /// IsAll means the filter should not restrict results.
    /// </summary>
    public sealed record FilterOption<T>(
        string Label,
        T Value,
        bool IsAll = false);

    public bool HasInvalidDateRange => FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date;
    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                OnPropertyChanged(nameof(HasInvalidDateRange));
                OnPropertyChanged(nameof(IsEmpty));
                ApplyFilters();
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                OnPropertyChanged(nameof(HasInvalidDateRange));
                ApplyFilters();
            }
        }
    }

    public AccountingEntriesViewModel(
        IAccountingService accountingService)
    {
        _accountingService = accountingService;

        GlAccountOptions.Add(
            new FilterOption<long>(
                "All GL accounts",
                default,
                true));

        EntryTypeOptions =
        [
            new("All entry types", default, true),
            new("Debit", EntryType.Debit),
            new("Credit", EntryType.Credit)
        ];

        _selectedGlAccount = GlAccountOptions[0];
        _selectedEntryType = EntryTypeOptions[0];

        RefreshCommand =
            new AsyncRelayCommand(
                async _ =>
                    await RefreshAsync());

        ResetFiltersCommand =
            new RelayCommand(
                _ => ResetFilters());
    }

    /// <summary>
    /// Rows currently visible in the table.
    /// </summary>
    public ObservableCollection<AccountingEntryResponse> Entries { get; } = [];

    /// <summary>
    /// GL accounts available for filtering.
    /// </summary>
    public ObservableCollection<FilterOption<long>> GlAccountOptions { get; } = [];

    /// <summary>
    /// Debit / credit filter options.
    /// </summary>
    public IReadOnlyList<FilterOption<EntryType>> EntryTypeOptions { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public RelayCommand ResetFiltersCommand { get; }

    public FilterOption<long>? SelectedGlAccount
    {
        get => _selectedGlAccount;
        set
        {
            if (SetProperty(ref _selectedGlAccount, value))
            {
                ApplyFilters();
            }
        }
    }

    public FilterOption<EntryType>? SelectedEntryType
    {
        get => _selectedEntryType;
        set
        {
            if (SetProperty(ref _selectedEntryType, value))
            {
                ApplyFilters();
            }
        }
    }

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

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set =>
            SetProperty(ref _isRefreshing, value);
    }

    public string? ErrorMessage
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

    public bool HasError =>
        !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty =>
        !IsLoading &&
        !HasInvalidDateRange &&
        Entries.Count == 0;

    public string CountText =>
        Entries.Count == 1
            ? "1 entry"
            : $"{Entries.Count} entries";

    /// <summary>
    /// Loads Accounting Entries when the page first opens.
    /// </summary>
    public Task InitializeAsync(
        CancellationToken cancellationToken)
    {
        return LoadAsync(cancellationToken);
    }

    /// <summary>
    /// Performs the initial page load.
    /// </summary>
    private async Task LoadAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var entriesTask =
                _accountingService.GetAccountingEntriesAsync(
                    null,
                    null,
                    null,
                    null,
                    cancellationToken);

            var glAccountsTask =
                _accountingService.GetGlAccountsAsync(
                    cancellationToken);

            await Task.WhenAll(
                entriesTask,
                glAccountsTask);

            UpdateEntries(
                await entriesTask);

            UpdateGlAccountOptions(
                await glAccountsTask);

            ApplyFilters();
        }
        catch (AppException exception)
        {
            ErrorMessage =
                exception.Message;
        }
        finally
        {
            IsLoading = false;

            OnPropertyChanged(
                nameof(IsEmpty));

            OnPropertyChanged(
                nameof(CountText));
        }
    }

    /// <summary>
    /// Reloads the latest accounting entries without showing
    /// the full page loading state.
    /// </summary>
    private async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            ErrorMessage = null;

            var entries =
                await _accountingService
                    .GetAccountingEntriesAsync(
                        null,
                        null,
                        null,
                        null,
                        CancellationToken.None);

            UpdateEntries(entries);

            ApplyFilters();
        }
        catch (AppException exception)
        {
            ErrorMessage =
                exception.Message;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Replaces the local entry cache with the latest server data.
    /// </summary>
    private void UpdateEntries(
        IReadOnlyList<AccountingEntryResponse> entries)
    {
        _allEntries.Clear();
        _allEntries.AddRange(entries);
    }

    /// <summary>
    /// Builds readable GL account filter options.
    /// </summary>
    private void UpdateGlAccountOptions(
        IReadOnlyList<GlAccountResponse> accounts)
    {
        var selectedId =
            SelectedGlAccount is not null &&
            !SelectedGlAccount.IsAll
                ? SelectedGlAccount.Value
                : (long?)null;

        GlAccountOptions.Clear();

        GlAccountOptions.Add(
            new FilterOption<long>(
                "All GL accounts",
                default,
                true));

        foreach (var account in accounts)
        {
            GlAccountOptions.Add(
                new FilterOption<long>(
                    $"{account.Code} - {account.Name}",
                    account.Id));
        }

        if (selectedId.HasValue)
        {
            SelectedGlAccount =
                GlAccountOptions.FirstOrDefault(
                    option =>
                        !option.IsAll &&
                        option.Value ==
                        selectedId.Value)
                ?? GlAccountOptions[0];
        }
        else
        {
            SelectedGlAccount =
                GlAccountOptions[0];
        }
    }

    /// <summary>
    /// Applies the same local filtering pattern used by
    /// the General Ledger screen.
    /// </summary>
    private void ApplyFilters()
    {
        if (HasInvalidDateRange)
        {
            Entries.Clear();

            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(CountText));

            return;
        }

        IEnumerable<AccountingEntryResponse> query =
            _allEntries;

        if (FromDate.HasValue)
        {
            var fromDate =
                DateOnly.FromDateTime(
                    FromDate.Value);

            query =
                query.Where(
                    entry =>
                        entry.PostingDate >=
                        fromDate);
        }

        if (ToDate.HasValue)
        {
            var toDate =
                DateOnly.FromDateTime(
                    ToDate.Value);

            query =
                query.Where(
                    entry =>
                        entry.PostingDate <=
                        toDate);
        }

        if (SelectedGlAccount is not null &&
            !SelectedGlAccount.IsAll)
        {
            query =
                query.Where(
                    entry =>
                        entry.GlAccountId ==
                        SelectedGlAccount.Value);
        }

        if (SelectedEntryType is not null &&
            !SelectedEntryType.IsAll)
        {
            query =
                query.Where(
                    entry =>
                        entry.EntryType ==
                        SelectedEntryType.Value);
        }

        Entries.Clear();

        foreach (var entry in query)
        {
            Entries.Add(entry);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CountText));
    }

    /// <summary>
    /// Clears filters without making another server request.
    /// </summary>
    private void ResetFilters()
    {
        FromDate = null;
        ToDate = null;

        SelectedGlAccount =
            GlAccountOptions.Count > 0
                ? GlAccountOptions[0]
                : null;

        SelectedEntryType =
            EntryTypeOptions[0];
    }
}