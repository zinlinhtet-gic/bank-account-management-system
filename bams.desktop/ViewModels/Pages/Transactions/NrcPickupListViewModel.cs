using System.Collections.ObjectModel;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// The NRC pickup tab of the Transactions page: NRC transfers waiting to be collected at our branches, searchable by
/// the receiver the sender designated. Picking one up opens the same pickup form as Transaction History, where the
/// officer still enters the identity from the collector's NRC card and the code. A newer load cancels one still
/// running, so quickly searching again never shows an older result last.
/// </summary>
public sealed class NrcPickupListViewModel : ViewModelBase
{
    private const int FirstPage = 1;

    private readonly ITransactionService _transactionService;
    private readonly IDialogService _dialogService;

    private CancellationTokenSource? _loadCancellation;
    private string _receiverNameSearch = string.Empty;
    private string _appliedReceiverName = string.Empty;
    private bool _isLoading;
    private int _page = FirstPage;
    private int _totalCount;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    public NrcPickupListViewModel(ITransactionService transactionService, IDialogService dialogService)
    {
        // Constructors only store dependencies and create commands. No server calls here.
        _transactionService = transactionService;
        _dialogService = dialogService;

        SearchCommand = new AsyncRelayCommand(SearchAsync);
        ClearSearchCommand = new AsyncRelayCommand(ClearSearchAsync, () => HasSearch);
        RefreshCommand = new AsyncRelayCommand(() => ReloadAsync(Page, keepMessages: false));
        PreviousPageCommand = new AsyncRelayCommand(() => ReloadAsync(Page - 1, keepMessages: false), () => CanGoToPreviousPage);
        NextPageCommand = new AsyncRelayCommand(() => ReloadAsync(Page + 1, keepMessages: false), () => CanGoToNextPage);
        // Disabled through CanExecute, not IsEnabled, so the button style can still lock it without a teller session.
        PickUpCommand = new AsyncRelayCommand(PickUpAsync, row => row is NrcPickupDisplayModel { CanPickUp: true });
    }

    public ObservableCollection<NrcPickupDisplayModel> Pickups { get; } = [];

    /// <summary>Receiver name as typed; applied by <see cref="SearchCommand"/> (Enter or the search icon).</summary>
    public string ReceiverNameSearch
    {
        get => _receiverNameSearch;
        set => SetProperty(ref _receiverNameSearch, value);
    }

    public int PersonNameMaximumLength => TransactionFieldRules.PersonNameMaximumLength;

    public AsyncRelayCommand SearchCommand { get; }

    /// <summary>Clears the receiver name and lists every waiting pickup again.</summary>
    public AsyncRelayCommand ClearSearchCommand { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    public AsyncRelayCommand PreviousPageCommand { get; }

    public AsyncRelayCommand NextPageCommand { get; }

    /// <summary>Row action: parameter is the clicked <see cref="NrcPickupDisplayModel"/>.</summary>
    public AsyncRelayCommand PickUpCommand { get; }

    /// <summary>A receiver name filter is applied to the rows shown.</summary>
    public bool HasSearch => _appliedReceiverName.Length > 0;

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

    /// <summary>True when a finished load returned no rows.</summary>
    public bool IsEmpty => !IsLoading && Pickups.Count == 0;

    public string EmptyTitle => HasSearch
        ? $"No waiting pickups for \"{_appliedReceiverName}\""
        : "No NRC transfers are waiting for pickup";

    public string EmptyHint => HasSearch
        ? "Check the spelling, or search with part of the name."
        : "New NRC transfers collected at a branch appear here.";

    public int Page
    {
        get => _page;
        private set => SetProperty(ref _page, value);
    }

    /// <summary>Counter next to the table title, e.g. "3 waiting".</summary>
    public string CountText => $"{_totalCount} waiting";

    /// <summary>e.g. "Page 2 of 3".</summary>
    public string PageText => $"Page {Page} of {TotalPages}";

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

    private int TotalPages => Math.Max(FirstPage, (int)Math.Ceiling(_totalCount / (double)TransactionFieldRules.PageSize));

    private bool CanGoToPreviousPage => !IsLoading && Page > FirstPage;

    private bool CanGoToNextPage => !IsLoading && Page < TotalPages;

    /// <summary>Called by the Transactions page each time the tab is opened: clears the search and lists page 1.</summary>
    public Task LoadAsync()
    {
        ReceiverNameSearch = string.Empty;
        SetAppliedReceiverName(string.Empty);
        return ReloadAsync(FirstPage, keepMessages: false);
    }

    private Task SearchAsync()
    {
        SetAppliedReceiverName(ReceiverNameSearch.Trim());
        return ReloadAsync(FirstPage, keepMessages: false);
    }

    private Task ClearSearchAsync()
    {
        ReceiverNameSearch = string.Empty;
        return SearchAsync();
    }

    // Loads one page with the applied search and replaces the rows only after the call succeeds; shows a failure in
    // the banner. Returns quietly when a newer load replaced this one.
    private async Task ReloadAsync(int page, bool keepMessages)
    {
        if (!keepMessages)
        {
            ClearMessages();
        }

        _loadCancellation?.Cancel();
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;

        try
        {
            IsLoading = true;
            RaisePagingChanged();

            var result = await _transactionService.GetPendingNrcPickupsAsync(
                _appliedReceiverName,
                Math.Max(FirstPage, page),
                TransactionFieldRules.PageSize,
                cancellation.Token);

            Pickups.Clear();
            foreach (var pickup in result.Items)
            {
                Pickups.Add(NrcPickupDisplayModel.FromResponse(pickup));
            }

            Page = result.PageNumber;
            _totalCount = result.TotalCount;
            OnPropertyChanged(nameof(CountText));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Replaced by a newer load.
        }
        catch (AppException exception)
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                ErrorMessage = exception.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _loadCancellation = null;
                IsLoading = false;
                RaisePagingChanged();
            }
        }
    }

    // Loads the transfer and opens the pickup form (collector's NRC card and code, cash payout). Reloads even when
    // cancelled, since wrong codes may have blocked the transfer meanwhile, then confirms a completed pickup.
    private async Task PickUpAsync(object? parameter)
    {
        if (parameter is not NrcPickupDisplayModel { CanPickUp: true } row)
        {
            return;
        }

        ClearMessages();

        DTOs.Transactions.TransactionDetailResponse transfer;
        try
        {
            transfer = await _transactionService.GetTransactionByIdAsync(row.TransactionId, CancellationToken.None);
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
            return;
        }

        var completed = _dialogService.ShowDialog(new NrcPickupFormViewModel(_transactionService, transfer));

        await ReloadAsync(Page, keepMessages: false);

        if (completed)
        {
            SuccessMessage = $"{MessageCatalog.GetMessage(MessageCode.TransactionCompletedSuccessfully)} "
                + $"{row.TransactionNo} was picked up: {TransactionDisplay.FormatMoney(row.Amount)} paid out in cash.";
        }
    }

    // The empty-state texts and the Clear button depend on the applied search.
    private void SetAppliedReceiverName(string receiverName)
    {
        _appliedReceiverName = receiverName;
        OnPropertyChanged(nameof(HasSearch));
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyHint));
        ClearSearchCommand.RaiseCanExecuteChanged();
    }

    // The pager text and buttons depend on the page, total and loading state.
    private void RaisePagingChanged()
    {
        OnPropertyChanged(nameof(PageText));
        OnPropertyChanged(nameof(IsEmpty));
        PreviousPageCommand.RaiseCanExecuteChanged();
        NextPageCommand.RaiseCanExecuteChanged();
    }

    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
    }
}
