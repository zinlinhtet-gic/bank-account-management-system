using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounting;
using bams.desktop.DTOs.Common;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Accounting;

public sealed class AccountingEntriesViewModel : ViewModelBase, IAsyncInitializable
{
    private const int PageSize = 10;
    private readonly IAccountingService _accountingService;
    private readonly IAccountingExportService _exportService;
    private readonly IDialogService _dialogService;
    private bool _defaultsToCurrentMonth;
    private CancellationTokenSource? _loadCancellation;
    private bool _isLoading;
    private bool _isRefreshing;
    private bool _hasLoaded;
    private int _currentPage = 1;
    private int _totalCount;
    private string? _errorMessage;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private FilterOption<long>? _selectedGlAccount;
    private FilterOption<EntryType>? _selectedEntryType;

    public sealed record FilterOption<T>(string Label, T Value, bool IsAll = false);

    public AccountingEntriesViewModel(
        IAccountingService accountingService,
        IAccountingExportService exportService,
        IDialogService dialogService)
    {
        _accountingService = accountingService;
        _exportService = exportService;
        _dialogService = dialogService;
        GlAccountOptions.Add(new("All GL accounts", default, true));
        EntryTypeOptions = [new("All entry types", default, true), new("Debit", EntryType.Debit), new("Credit", EntryType.Credit)];
        _selectedGlAccount = GlAccountOptions[0];
        _selectedEntryType = EntryTypeOptions[0];
        RefreshCommand = new AsyncRelayCommand(async _ => await RefreshAsync());
        ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
        ExportToExcelCommand = new AsyncRelayCommand(ExportToExcelAsync);
        PreviousPageCommand = new RelayCommand(_ => _ = LoadPageAsync(CurrentPage - 1), _ => HasPreviousPage && !IsLoading);
        NextPageCommand = new RelayCommand(_ => _ = LoadPageAsync(CurrentPage + 1), _ => HasNextPage && !IsLoading);
    }

    public ObservableCollection<AccountingEntryResponse> Entries { get; } = [];
    public ObservableCollection<AccountingTransactionGroupDisplayModel> TransactionGroups { get; } = [];
    public ObservableCollection<FilterOption<long>> GlAccountOptions { get; } = [];
    public IReadOnlyList<FilterOption<EntryType>> EntryTypeOptions { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }
    public AsyncRelayCommand ExportToExcelCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public int CurrentPage => _currentPage;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));
    public string PageInfo => $"Page {CurrentPage} of {TotalPages}";
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasInvalidDateRange => FromDate.HasValue && ToDate.HasValue && FromDate.Value.Date > ToDate.Value.Date;

    public DateTime? FromDate
    {
        get => _fromDate;
        set { if (SetProperty(ref _fromDate, value)) { OnPropertyChanged(nameof(HasInvalidDateRange)); if (_hasLoaded) _ = LoadPageAsync(1); } }
    }
    public DateTime? ToDate
    {
        get => _toDate;
        set { if (SetProperty(ref _toDate, value)) { OnPropertyChanged(nameof(HasInvalidDateRange)); if (_hasLoaded) _ = LoadPageAsync(1); } }
    }
    public FilterOption<long>? SelectedGlAccount
    {
        get => _selectedGlAccount;
        set { if (SetProperty(ref _selectedGlAccount, value) && _hasLoaded) _ = LoadPageAsync(1); }
    }
    public FilterOption<EntryType>? SelectedEntryType
    {
        get => _selectedEntryType;
        set { if (SetProperty(ref _selectedEntryType, value) && _hasLoaded) _ = LoadPageAsync(1); }
    }
    public bool IsLoading
    {
        get => _isLoading;
        private set { if (SetProperty(ref _isLoading, value)) { OnPropertyChanged(nameof(IsEmpty)); PreviousPageCommand.RaiseCanExecuteChanged(); NextPageCommand.RaiseCanExecuteChanged(); } }
    }
    public bool IsRefreshing { get => _isRefreshing; private set => SetProperty(ref _isRefreshing, value); }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmpty => !IsLoading && !HasInvalidDateRange && Entries.Count == 0;
    public string CountText => _totalCount == 1 ? "1 transaction" : $"{_totalCount} transactions";

    /// <summary>
    /// Makes this instance the "Current Month Entries" view: the date filters start at the first and last day of the
    /// current Myanmar business month, and Reset returns to them. Call before <see cref="InitializeAsync"/>.
    /// </summary>
    public void UseCurrentMonthDefault()
    {
        _defaultsToCurrentMonth = true;
        ApplyCurrentMonthDates();
    }

    // Sets the date filters to the current Myanmar business month without triggering a reload.
    private void ApplyCurrentMonthDates()
    {
        var today = MyanmarTimeDisplay.ToMyanmarTime(DateTime.UtcNow);
        SetMonthDates(today.Year, today.Month);
    }

    // Writes the first and last day of a month into the date filters through the backing fields (no reload).
    private void SetMonthDates(int year, int month)
    {
        var firstDay = new DateTime(year, month, 1);
        _fromDate = firstDay;
        _toDate = firstDay.AddMonths(1).AddDays(-1);
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));
        OnPropertyChanged(nameof(HasInvalidDateRange));
        OnPropertyChanged(nameof(HighlightHint));
    }

    /// <summary>
    /// Shows every entry posted in the given month (used when a Monthly Summaries row is clicked).
    /// Loads GL accounts first if this tab has not been opened yet.
    /// </summary>
    public async Task LoadMonthAsync(int year, int month, CancellationToken cancellationToken)
    {
        SetMonthDates(year, month);
        SelectedGlAccountSilently(GlAccountOptions[0]);
        if (_hasLoaded)
        {
            await LoadPageAsync(1);
            return;
        }

        await InitializeAsync(cancellationToken);
    }

    // Selects a GL account option without starting a reload (the caller reloads once afterwards).
    private void SelectedGlAccountSilently(FilterOption<long> option)
    {
        if (SetProperty(ref _selectedGlAccount, option, nameof(SelectedGlAccount)))
        {
            OnPropertyChanged(nameof(HighlightHint));
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            var accountsTask = _accountingService.GetGlAccountsAsync(cancellationToken);
            // Use the current filters so a month tab opens already narrowed to its month.
            var entriesTask = _accountingService.GetAccountingEntriesAsync(
                FromDate.HasValue ? DateOnly.FromDateTime(FromDate.Value) : null,
                ToDate.HasValue ? DateOnly.FromDateTime(ToDate.Value) : null,
                null, null, 1, PageSize, cancellationToken);
            await Task.WhenAll(accountsTask, entriesTask);
            foreach (var account in await accountsTask) GlAccountOptions.Add(new($"{account.Code} - {account.Name}", account.Id));
            UpdatePage(await entriesTask);
            _hasLoaded = true;
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsLoading = false; OnPropertyChanged(nameof(IsEmpty)); }
    }

    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try { await LoadPageAsync(1); }
        finally { IsRefreshing = false; }
    }

    private async Task LoadPageAsync(int page)
    {
        if (page < 1 || (page > TotalPages && page != 1)) return;
        if (HasInvalidDateRange)
        {
            Entries.Clear(); TransactionGroups.Clear(); _totalCount = 0; _currentPage = 1;
            OnPropertyChanged(nameof(IsEmpty)); OnPropertyChanged(nameof(CountText)); OnPropertyChanged(nameof(PageInfo));
            return;
        }

        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        IsLoading = true;
        try
        {
            var result = await _accountingService.GetAccountingEntriesAsync(
                FromDate.HasValue ? DateOnly.FromDateTime(FromDate.Value) : null,
                ToDate.HasValue ? DateOnly.FromDateTime(ToDate.Value) : null,
                SelectedGlAccount is { IsAll: false } gl ? gl.Value : null,
                SelectedEntryType is { IsAll: false } type ? type.Value : null,
                page, PageSize, cancellation.Token);
            UpdatePage(result);
            ErrorMessage = null;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null; IsLoading = false;
                PreviousPageCommand.RaiseCanExecuteChanged(); NextPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private void UpdatePage(PagedResponse<AccountingEntryResponse> result)
    {
        Entries.Clear(); foreach (var entry in result.Items) Entries.Add(entry);
        TransactionGroups.Clear();
        foreach (var group in AccountingTransactionGroupDisplayModel.BuildGroups(result.Items, CreateFilterMatcher()))
            TransactionGroups.Add(group);
        OnPropertyChanged(nameof(HighlightHint));
        _currentPage = result.PageNumber; 
        _totalCount = result.TotalCount;
        OnPropertyChanged(nameof(CurrentPage)); OnPropertyChanged(nameof(TotalPages)); OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(HasPreviousPage)); OnPropertyChanged(nameof(HasNextPage)); OnPropertyChanged(nameof(CountText)); OnPropertyChanged(nameof(IsEmpty));
        PreviousPageCommand.RaiseCanExecuteChanged(); NextPageCommand.RaiseCanExecuteChanged();
    }

    // True when any filter narrows the result; only then are matching journal lines highlighted.
    private bool HasActiveFilter =>
        FromDate.HasValue || ToDate.HasValue ||
        SelectedGlAccount is { IsAll: false } || SelectedEntryType is { IsAll: false };

    /// <summary>Legend text for highlighted lines, or null when no filter is active.</summary>
    public string? HighlightHint => HasActiveFilter ? "Highlighted lines match your filter." : null;

    // The server returns every line of each matching transaction; this marks the lines that met the filter.
    private Func<AccountingEntryResponse, bool>? CreateFilterMatcher()
    {
        if (!HasActiveFilter)
        {
            return null;
        }

        var fromDate = FromDate.HasValue ? DateOnly.FromDateTime(FromDate.Value) : (DateOnly?)null;
        var toDate = ToDate.HasValue ? DateOnly.FromDateTime(ToDate.Value) : (DateOnly?)null;
        var glAccountId = SelectedGlAccount is { IsAll: false } gl ? gl.Value : (long?)null;
        var entryType = SelectedEntryType is { IsAll: false } type ? type.Value : (EntryType?)null;

        return entry =>
            (fromDate is null || entry.PostingDate >= fromDate) &&
            (toDate is null || entry.PostingDate <= toDate) &&
            (glAccountId is null || entry.GlAccountId == glAccountId) &&
            (entryType is null || entry.EntryType == entryType);
    }

    // Clears every filter; the Current Month tab goes back to the current month instead of "all dates".
    private void ResetFilters()
    {
        if (_defaultsToCurrentMonth)
        {
            // Date fields are set silently here, so reload once explicitly after the other filters reset.
            ApplyCurrentMonthDates();
        }
        else
        {
            FromDate = null; ToDate = null;
        }
        SelectedGlAccount = GlAccountOptions[0];
        SelectedEntryType = EntryTypeOptions[0];
        if (_defaultsToCurrentMonth && _hasLoaded)
        {
            _ = LoadPageAsync(1);
        }
    }

    // Asks for the export options, then downloads and saves the Excel file; outcome is shown in a message dialog.
    private async Task ExportToExcelAsync()
    {
        var dialog = new ExportAccountingDialogViewModel(_dialogService, _accountingService);
        await dialog.LoadAccountsAsync(CancellationToken.None);
        if (!_dialogService.ShowDialog(dialog))
        {
            return;
        }

        try
        {
            ErrorMessage = null;
            await _exportService.ExportToExcelAsync(dialog.BuildRequest(), CancellationToken.None);
            _dialogService.ShowMessage("Export complete", $"Saved to {dialog.FilePath}");
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
    }
}
