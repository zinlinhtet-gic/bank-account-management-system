using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

public sealed class GLAccountDetailViewModel : ViewModelBase, IAsyncInitializable
{
    private const int PageSize = 10;
    private readonly IAccountingService _accountingService;
    private long _accountId;
    private bool _isLoading;
    private int _page = 1;
    private int _totalCount;
    private string? _errorMessage;

    public GLAccountDetailViewModel(IAccountingService accountingService)
    {
        _accountingService = accountingService;
        BackCommand = new RelayCommand(_ => BackRequested?.Invoke());
        PreviousPageCommand = new RelayCommand(_ => _ = LoadPageAsync(Page - 1), _ => Page > 1 && !IsLoading);
        NextPageCommand = new RelayCommand(_ => _ = LoadPageAsync(Page + 1), _ => Page < TotalPages && !IsLoading);
    }

    public event Action? BackRequested;
    public RelayCommand BackCommand { get; }
    public RelayCommand PreviousPageCommand { get; }
    public RelayCommand NextPageCommand { get; }
    public GlAccountResponse? Account { get; private set; }
    public ObservableCollection<AccountingEntryResponse> Entries { get; } = [];
    public ObservableCollection<AccountingTransactionGroupDisplayModel> TransactionGroups { get; } = [];
    public int Page => _page;
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(_totalCount / (double)PageSize));
    public string PageInfo => $"Page {Page} of {TotalPages}";
    public bool IsLoading { get => _isLoading; private set { if (SetProperty(ref _isLoading, value)) { OnPropertyChanged(nameof(IsEmpty)); PreviousPageCommand.RaiseCanExecuteChanged(); NextPageCommand.RaiseCanExecuteChanged(); } } }
    public string? ErrorMessage { get => _errorMessage; private set { if (SetProperty(ref _errorMessage, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsEmpty => !IsLoading && !HasError && Entries.Count == 0;
    public string CountText => _totalCount == 1 ? "1 transaction" : $"{_totalCount} transactions";
    public void SetAccountId(long accountId) => _accountId = accountId;

    public Task InitializeAsync(CancellationToken cancellationToken) => LoadPageAsync(1, cancellationToken);

    private async Task LoadPageAsync(int page, CancellationToken cancellationToken = default)
    {
        if (page < 1 || (page > TotalPages && page != 1)) return;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var detail = await _accountingService.GetGlAccountDetailAsync(_accountId, page, PageSize, cancellationToken);
            Account = detail.Account;
            OnPropertyChanged(nameof(Account));
            Entries.Clear();
            foreach (var entry in detail.Entries.Items) Entries.Add(entry);
            TransactionGroups.Clear();
            foreach (var group in detail.Entries.Items.GroupBy(entry => entry.TransactionId).OrderByDescending(group => group.First().TransactionAt).ThenByDescending(group => group.Key))
                TransactionGroups.Add(new AccountingTransactionGroupDisplayModel(group.OrderBy(entry => entry.Id).ToList()));
            _page = detail.Entries.Page;
            _totalCount = detail.Entries.TotalCount;
            OnPropertyChanged(nameof(Page)); OnPropertyChanged(nameof(TotalPages)); OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(CountText)); OnPropertyChanged(nameof(IsEmpty));
        }
        catch (AppException exception) { ErrorMessage = exception.Message; }
        finally { IsLoading = false; }
    }
}
