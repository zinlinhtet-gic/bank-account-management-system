using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.DTOs.Common;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

public sealed class AccountingEntriesViewModel : ViewModelBase, IAsyncInitializable
{
    private const int PageSize = 10;
    private readonly IAccountingService _accountingService;
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

    public AccountingEntriesViewModel(IAccountingService accountingService)
    {
        _accountingService = accountingService;
        GlAccountOptions.Add(new("All GL accounts", default, true));
        EntryTypeOptions = [new("All entry types", default, true), new("Debit", EntryType.Debit), new("Credit", EntryType.Credit)];
        _selectedGlAccount = GlAccountOptions[0];
        _selectedEntryType = EntryTypeOptions[0];
        RefreshCommand = new AsyncRelayCommand(async _ => await RefreshAsync());
        ResetFiltersCommand = new RelayCommand(_ => ResetFilters());
        PreviousPageCommand = new RelayCommand(_ => _ = LoadPageAsync(CurrentPage - 1), _ => HasPreviousPage && !IsLoading);
        NextPageCommand = new RelayCommand(_ => _ = LoadPageAsync(CurrentPage + 1), _ => HasNextPage && !IsLoading);
    }

    public ObservableCollection<AccountingEntryResponse> Entries { get; } = [];
    public ObservableCollection<AccountingTransactionGroupDisplayModel> TransactionGroups { get; } = [];
    public ObservableCollection<FilterOption<long>> GlAccountOptions { get; } = [];
    public IReadOnlyList<FilterOption<EntryType>> EntryTypeOptions { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ResetFiltersCommand { get; }
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

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            var accountsTask = _accountingService.GetGlAccountsAsync(cancellationToken);
            var entriesTask = _accountingService.GetAccountingEntriesAsync(null, null, null, null, 1, PageSize, cancellationToken);
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
        foreach (var group in result.Items.GroupBy(entry => entry.TransactionId).OrderByDescending(group => group.First().TransactionAt).ThenByDescending(group => group.Key))
            TransactionGroups.Add(new AccountingTransactionGroupDisplayModel(group.OrderBy(entry => entry.Id).ToList()));
        _currentPage = result.PageNumber; _totalCount = result.TotalCount;
        OnPropertyChanged(nameof(CurrentPage)); OnPropertyChanged(nameof(TotalPages)); OnPropertyChanged(nameof(PageInfo));
        OnPropertyChanged(nameof(HasPreviousPage)); OnPropertyChanged(nameof(HasNextPage)); OnPropertyChanged(nameof(CountText)); OnPropertyChanged(nameof(IsEmpty));
        PreviousPageCommand.RaiseCanExecuteChanged(); NextPageCommand.RaiseCanExecuteChanged();
    }

    private void ResetFilters()
    {
        FromDate = null; ToDate = null;
        SelectedGlAccount = GlAccountOptions[0];
        SelectedEntryType = EntryTypeOptions[0];
    }
}
