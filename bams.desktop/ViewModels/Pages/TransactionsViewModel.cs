using bams.desktop.Commands;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;
using bams.desktop.ViewModels.Pages.Transactions;

namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// ViewModel for the Transactions page (officers): one tab per kind of posting (deposit, withdrawal, internal,
/// interbank and NRC transfer) with its form shown inline. After a posting the tab gets a fresh form with fresh
/// balances. Existing transactions and their pending actions live on the Transaction History page.
/// </summary>
public sealed class TransactionsViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly ITransactionService _transactionService;
    private readonly IAccountService _accountService;
    private readonly IDialogService _dialogService;

    private IReadOnlyList<OtherBankResponse> _banks = [];
    private IReadOnlyList<BranchResponse> _branches = [];
    private TransactionFormViewModel? _currentForm;
    private bool _isLoadingForm;
    private int _formLoadVersion;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;

    public TransactionsViewModel(
        ITransactionService transactionService,
        IAccountService accountService,
        IDialogService dialogService)
    {
        // Constructors only store dependencies and create commands. No server calls here.
        _transactionService = transactionService;
        _accountService = accountService;
        _dialogService = dialogService;

        Tabs =
        [
            new(TransactionFormKind.Deposit, "Deposit", "Icon.Download", "Cash paid into a customer account"),
            new(TransactionFormKind.Withdrawal, "Withdraw", "Icon.Upload", "Cash paid out of a customer account"),
            new(TransactionFormKind.InternalTransfer, "Internal transfer", "Icon.Transfers", "Between two accounts in this bank"),
            new(TransactionFormKind.InterbankTransfer, "Interbank transfer", "Icon.Bank", "To an account at another bank"),
            new(TransactionFormKind.NrcTransfer, "NRC transfer", "Icon.Send", "Collected by a named receiver with their NRC and a pickup code")
        ];
        foreach (var tab in Tabs)
        {
            tab.Selected += OnTabSelected;
        }

        ReloadFormCommand = new AsyncRelayCommand(() => OpenFormAsync(SelectedTab.Kind, keepMessages: false));
    }

    // Kept for the placeholder view; the header already shows the page name.
    public string PageTitle => "Transactions";
    public string PageDescription => "Post deposits, withdrawals and transfers";

    public IReadOnlyList<TransactionTabViewModel> Tabs { get; }

    public TransactionTabViewModel SelectedTab => Tabs.FirstOrDefault(tab => tab.IsSelected) ?? Tabs[0];

    /// <summary>The form of the selected tab; null while it loads or when loading failed.</summary>
    public TransactionFormViewModel? CurrentForm
    {
        get => _currentForm;
        private set
        {
            if (SetProperty(ref _currentForm, value))
            {
                OnPropertyChanged(nameof(HasForm));
                OnPropertyChanged(nameof(ShowsFormUnavailable));
            }
        }
    }

    public bool HasForm => CurrentForm is not null;

    public bool IsLoadingForm
    {
        get => _isLoadingForm;
        private set
        {
            if (SetProperty(ref _isLoadingForm, value))
            {
                OnPropertyChanged(nameof(ShowsFormUnavailable));
            }
        }
    }

    /// <summary>The form could not be opened (e.g. accounts failed to load); the view offers "Try again".</summary>
    public bool ShowsFormUnavailable => !IsLoadingForm && CurrentForm is null;

    /// <summary>Opens the selected tab's form again, e.g. after a failed load.</summary>
    public AsyncRelayCommand ReloadFormCommand { get; }

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

    // Called by MainViewModel every time the user opens this page: opens the first tab.
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        Tabs[0].SelectSilently();
        return OpenFormAsync(Tabs[0].Kind, keepMessages: false);
    }

    // async void is intentional: an event handler; OpenFormAsync catches every expected failure itself.
    private async void OnTabSelected(TransactionTabViewModel tab)
    {
        // RadioButtons uncheck the previous tab themselves; keep the ViewModels in step.
        foreach (var other in Tabs.Where(other => other != tab && other.IsSelected))
        {
            other.IsSelected = false;
        }

        OnPropertyChanged(nameof(SelectedTab));
        await OpenFormAsync(tab.Kind, keepMessages: false);
    }

    // Loads fresh account balances (and banks / branches when the kind needs them) and shows a new form.
    // A newer open replaces one still loading, so quickly switching tabs never shows an older form last.
    private async Task OpenFormAsync(TransactionFormKind kind, bool keepMessages)
    {
        var version = ++_formLoadVersion;
        if (!keepMessages)
        {
            ClearMessages();
        }

        ReplaceForm(null);
        IsLoadingForm = true;

        try
        {
            var accounts = await AccountOption.LoadUsableAsync(_accountService, CancellationToken.None);
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

            if (version == _formLoadVersion)
            {
                ReplaceForm(new TransactionFormViewModel(_transactionService, kind, accounts, _banks, _branches));
            }
        }
        catch (AppException exception)
        {
            if (version == _formLoadVersion)
            {
                ErrorMessage = exception.Message;
            }
        }
        finally
        {
            if (version == _formLoadVersion)
            {
                IsLoadingForm = false;
            }
        }
    }

    // Swaps the shown form, moving the event subscriptions so a replaced form cannot report back.
    private void ReplaceForm(TransactionFormViewModel? form)
    {
        if (CurrentForm is not null)
        {
            CurrentForm.Posted -= OnFormPosted;
            CurrentForm.ClearRequested -= OnFormClearRequested;
        }

        if (form is not null)
        {
            form.Posted += OnFormPosted;
            form.ClearRequested += OnFormClearRequested;
        }

        CurrentForm = form;
    }

    // async void is intentional: an event handler. Confirms the posting, shows the one-time NRC pickup code, and
    // replaces the form with a fresh one (new idempotency key, updated balances).
    private async void OnFormPosted()
    {
        var form = CurrentForm;
        if (form?.SavedTransaction is not { } saved)
        {
            return;
        }

        var outcome = saved.TransactionStatus == TransactionStatus.Pending
            ? MessageCode.TransactionSubmittedSuccessfully
            : MessageCode.TransactionCompletedSuccessfully;
        ErrorMessage = string.Empty;
        SuccessMessage = $"{MessageCatalog.GetMessage(outcome)} {DescribeTransaction(saved)}";

        // The code exists only in this response; show it before anything else so it cannot be missed.
        if (form.Kind == TransactionFormKind.NrcTransfer)
        {
            _dialogService.ShowDialog(new NrcPickupCodeViewModel(saved, form.ReceiverName.Trim(), form.PickupLocationText));
        }

        await OpenFormAsync(form.Kind, keepMessages: true);
    }

    // async void is intentional: an event handler; the officer starts over with an empty form.
    private async void OnFormClearRequested()
    {
        if (CurrentForm is { } form)
        {
            await OpenFormAsync(form.Kind, keepMessages: false);
        }
    }

    // Loads the branches once per page visit; returns false (with a banner) when there are none.
    private async Task<bool> EnsureBranchesLoadedAsync()
    {
        if (_branches.Count == 0)
        {
            _branches = await _transactionService.GetBranchesAsync(CancellationToken.None);
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
            _banks = await _transactionService.GetOtherBanksAsync(CancellationToken.None);
        }

        if (_banks.Count == 0 && isRequired)
        {
            ErrorMessage = "No destination banks are set up yet, so interbank transfers are not available.";
            return false;
        }

        return true;
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
