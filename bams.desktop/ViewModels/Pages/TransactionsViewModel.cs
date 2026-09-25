using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages.Transactions;

namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// ViewModel for the Transactions page (officers): post deposits, withdrawals and transfers, and finish pending
/// transfers (NRC pickup or cancel, interbank settled or failed). Coordinates the <see cref="Filter"/> and
/// <see cref="List"/> components and opens the form, action and detail dialogs.
/// </summary>
public sealed class TransactionsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ITransactionService _transactionService;
    private readonly IAccountService _accountService;
    private readonly IDialogService _dialogService;
    private readonly TransactionDetailsLauncher _detailsLauncher;

    private IReadOnlyList<OtherBankResponse> _banks = [];
    private IReadOnlyList<BranchResponse> _branches = [];
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    public TransactionsViewModel(
        ITransactionService transactionService,
        IAccountService accountService,
        IDialogService dialogService,
        TransactionDetailsLauncher detailsLauncher,
        TransactionFilterViewModel filter,
        TransactionListViewModel list)
    {
        // Constructors only store dependencies and create commands. No server calls here.
        _transactionService = transactionService;
        _accountService = accountService;
        _dialogService = dialogService;
        _detailsLauncher = detailsLauncher;
        Filter = filter;
        List = list;

        // The parent coordinates its components: a filter change reloads from page 1, the pager loads its page.
        Filter.FiltersChanged += OnFiltersChanged;
        List.PageRequested += OnPageRequested;

        RefreshCommand = new AsyncRelayCommand(() => ReloadAsync(List.Page, CancellationToken.None));
        ShowPendingCommand = new RelayCommand(_ => Filter.ShowOnly(null, TransactionStatus.Pending));
        NewDepositCommand = new AsyncRelayCommand(() => OpenTransactionFormAsync(TransactionFormKind.Deposit));
        NewWithdrawalCommand = new AsyncRelayCommand(() => OpenTransactionFormAsync(TransactionFormKind.Withdrawal));
        NewInternalTransferCommand = new AsyncRelayCommand(() => OpenTransactionFormAsync(TransactionFormKind.InternalTransfer));
        NewInterbankTransferCommand = new AsyncRelayCommand(() => OpenTransactionFormAsync(TransactionFormKind.InterbankTransfer));
        NewNrcTransferCommand = new AsyncRelayCommand(() => OpenTransactionFormAsync(TransactionFormKind.NrcTransfer));
        ShowDetailsCommand = new AsyncRelayCommand(ShowDetailsAsync);
        PickUpNrcTransferCommand = new AsyncRelayCommand(PickUpNrcTransferAsync);
        CancelNrcTransferCommand = new AsyncRelayCommand(row => FinishPendingTransferAsync(row, PendingTransferAction.CancelNrcTransfer));
        CompleteInterbankTransferCommand = new AsyncRelayCommand(row => FinishPendingTransferAsync(row, PendingTransferAction.CompleteInterbankTransfer));
        FailInterbankTransferCommand = new AsyncRelayCommand(row => FinishPendingTransferAsync(row, PendingTransferAction.FailInterbankTransfer));
    }

    // Kept for the placeholder view; the header already shows the page name.
    public string PageTitle => "Transactions";
    public string PageDescription => "Process and manage financial transactions";

    public TransactionFilterViewModel Filter { get; }

    public TransactionListViewModel List { get; }

    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>Shows every pending transfer, the ones waiting for pickup or a gateway result.</summary>
    public RelayCommand ShowPendingCommand { get; }

    public AsyncRelayCommand NewDepositCommand { get; }

    public AsyncRelayCommand NewWithdrawalCommand { get; }

    public AsyncRelayCommand NewInternalTransferCommand { get; }

    public AsyncRelayCommand NewInterbankTransferCommand { get; }

    public AsyncRelayCommand NewNrcTransferCommand { get; }

    /// <summary>Row click: parameter is the clicked <see cref="TransactionDisplayModel"/>.</summary>
    public AsyncRelayCommand ShowDetailsCommand { get; }

    /// <summary>
    /// Row action on a pending NRC transfer: pickup with the code at our branch, or recording the other bank's payout.
    /// </summary>
    public AsyncRelayCommand PickUpNrcTransferCommand { get; }

    /// <summary>Row action on a pending NRC transfer.</summary>
    public AsyncRelayCommand CancelNrcTransferCommand { get; }

    /// <summary>Row action on a pending interbank transfer.</summary>
    public AsyncRelayCommand CompleteInterbankTransferCommand { get; }

    /// <summary>Row action on a pending interbank transfer.</summary>
    public AsyncRelayCommand FailInterbankTransferCommand { get; }

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

    // Called by MainViewModel every time the user opens this page.
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        return ReloadAsync(TransactionListViewModel.FirstPageNumber, cancellationToken);
    }

    // async void is intentional: event handlers; ReloadAsync catches every expected failure itself.
    private async void OnFiltersChanged()
    {
        await ReloadAsync(TransactionListViewModel.FirstPageNumber, CancellationToken.None);
    }

    private async void OnPageRequested(int page)
    {
        await ReloadAsync(page, CancellationToken.None);
    }

    // Reloads the table page with the current filters and shows a failure in the banner.
    private async Task ReloadAsync(int page, CancellationToken cancellationToken)
    {
        var (filter, error) = Filter.BuildFilter();
        if (error is not null)
        {
            ErrorMessage = MessageCatalog.GetMessage(error.Value);
            return;
        }

        try
        {
            ErrorMessage = string.Empty;
            await List.LoadAsync(filter!, page, cancellationToken);
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
    }

    // Opens the form for a new posting with fresh account balances; after success reloads and confirms.
    private async Task OpenTransactionFormAsync(TransactionFormKind kind)
    {
        ClearMessages();

        var accounts = await LoadAccountOptionsAsync();
        if (accounts is null)
        {
            return;
        }

        if (kind == TransactionFormKind.InterbankTransfer && !await EnsureBanksLoadedAsync(isRequired: true))
        {
            return;
        }

        // NRC transfers are collected at one of our branches (required) or at another bank (optional choice).
        if (kind == TransactionFormKind.NrcTransfer
            && (!await EnsureBranchesLoadedAsync() || !await EnsureBanksLoadedAsync(isRequired: false)))
        {
            return;
        }

        var form = new TransactionFormViewModel(_transactionService, kind, accounts, _banks, _branches);
        if (!_dialogService.ShowDialog(form))
        {
            return;
        }

        var saved = form.SavedTransaction!;

        // The code exists only in this response; show it before anything else so it cannot be missed.
        if (kind == TransactionFormKind.NrcTransfer)
        {
            _dialogService.ShowDialog(new NrcPickupCodeViewModel(saved, form.ReceiverName.Trim(), form.PickupLocationText));
        }

        await ReloadAsync(TransactionListViewModel.FirstPageNumber, CancellationToken.None);

        var outcome = saved.TransactionStatus == TransactionStatus.Pending
            ? MessageCode.TransactionSubmittedSuccessfully
            : MessageCode.TransactionCompletedSuccessfully;
        SuccessMessage = $"{MessageCatalog.GetMessage(outcome)} {DescribeTransaction(saved)}";
    }

    // Loads the full record and shows it; the card can continue to an account statement.
    private async Task ShowDetailsAsync(object? parameter)
    {
        if (parameter is not TransactionDisplayModel row)
        {
            return;
        }

        ClearMessages();

        var transaction = await GetTransactionDetailAsync(row.Id);
        if (transaction is not null)
        {
            ErrorMessage = await _detailsLauncher.ShowAsync(transaction) ?? string.Empty;
        }
    }

    // Loads the transfer to see where it is collected. At our branch: the pickup form (receiver's NRC, code, cash or
    // account). At another bank: record that bank's payout. Then reloads and confirms.
    private async Task PickUpNrcTransferAsync(object? parameter)
    {
        if (parameter is not TransactionDisplayModel { IsPendingNrcTransfer: true } row)
        {
            return;
        }

        ClearMessages();

        var transfer = await GetTransactionDetailAsync(row.Id);
        if (transfer?.NrcTransfer is null)
        {
            return;
        }

        if (transfer.NrcTransfer.DeliveryType == TransactionFieldRules.NrcDeliveryAtOtherBank)
        {
            await FinishPendingTransferAsync(row, PendingTransferAction.RecordNrcPayout);
            return;
        }

        var accounts = await LoadAccountOptionsAsync();
        if (accounts is null)
        {
            return;
        }

        var form = new NrcPickupFormViewModel(_transactionService, transfer, accounts);
        var completed = _dialogService.ShowDialog(form);

        // Reload even when cancelled: wrong codes may have blocked the transfer meanwhile.
        await ReloadAsync(List.Page, CancellationToken.None);

        if (completed)
        {
            SuccessMessage = $"{MessageCatalog.GetMessage(MessageCode.TransactionCompletedSuccessfully)} "
                + $"{row.TransactionNo} was picked up: {TransactionDisplay.FormatMoney(row.Amount)} "
                + (form.IsPaidInCash ? "paid out in cash." : $"paid into {form.DestinationAccount!.AccountNo}.");
        }
    }

    // Cancels an NRC transfer or records an interbank result, then reloads and confirms.
    private async Task FinishPendingTransferAsync(object? parameter, PendingTransferAction action)
    {
        if (parameter is not TransactionDisplayModel row)
        {
            return;
        }

        ClearMessages();

        var form = new PendingTransferActionViewModel(_transactionService, row, action);
        if (!_dialogService.ShowDialog(form))
        {
            return;
        }

        await ReloadAsync(List.Page, CancellationToken.None);

        SuccessMessage = action switch
        {
            PendingTransferAction.CompleteInterbankTransfer =>
                $"{MessageCatalog.GetMessage(MessageCode.InterbankTransferSettledSuccessfully)} {row.TransactionNo}.",
            PendingTransferAction.RecordNrcPayout =>
                $"{MessageCatalog.GetMessage(MessageCode.TransactionCompletedSuccessfully)} {row.TransactionNo} was paid out by the other bank.",
            _ => $"{MessageCatalog.GetMessage(MessageCode.TransactionRefundedSuccessfully)} Refund {form.Result!.TransactionNo}."
        };
    }

    // Loads the accounts that can take part in a transaction; on failure shows the error and returns null.
    private async Task<IReadOnlyList<AccountOption>?> LoadAccountOptionsAsync()
    {
        try
        {
            var accounts = await _accountService.GetAccountsAsync(CancellationToken.None);

            return accounts.Where(AccountOption.IsUsable).Select(AccountOption.FromResponse).ToList();
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
            return null;
        }
    }

    // Loads the branches once per page visit; returns false (with a banner) when there are none.
    private async Task<bool> EnsureBranchesLoadedAsync()
    {
        if (_branches.Count == 0)
        {
            try
            {
                _branches = await _transactionService.GetBranchesAsync(CancellationToken.None);
            }
            catch (AppException exception)
            {
                ErrorMessage = exception.Message;
                return false;
            }
        }

        if (_branches.Count == 0)
        {
            ErrorMessage = "No branches are set up yet, so NRC transfers are not available.";
            return false;
        }

        return true;
    }

    // Loads the other banks once per page visit. When they are required (interbank transfers), returns false with a
    // banner if there are none; for NRC transfers they are only an optional pickup location.
    private async Task<bool> EnsureBanksLoadedAsync(bool isRequired)
    {
        if (_banks.Count == 0)
        {
            try
            {
                _banks = await _transactionService.GetOtherBanksAsync(CancellationToken.None);
            }
            catch (AppException exception)
            {
                ErrorMessage = exception.Message;
                return false;
            }
        }

        if (_banks.Count == 0 && isRequired)
        {
            ErrorMessage = "No destination banks are set up yet, so interbank transfers are not available.";
            return false;
        }

        return true;
    }

    // Loads one transaction; on failure shows the error and returns null.
    private async Task<TransactionDetailResponse?> GetTransactionDetailAsync(long transactionId)
    {
        try
        {
            return await _transactionService.GetTransactionByIdAsync(transactionId, CancellationToken.None);
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
            return null;
        }
    }

    // e.g. "Cash deposit TXN2026… · MMK 10,000.00."
    private static string DescribeTransaction(TransactionResponse transaction)
    {
        return $"{TransactionDisplay.ToDisplayName(transaction.TransactionType)} {transaction.TransactionNo} · "
            + $"{TransactionDisplay.FormatMoney(transaction.Amount)}.";
    }

    // A new action replaces the previous outcome banner.
    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
    }
}
