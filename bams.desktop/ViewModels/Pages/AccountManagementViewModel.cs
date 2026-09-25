using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounts;
using bams.desktop.DTOs.Customers;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;
using bams.desktop.ViewModels;

namespace bams.desktop.ViewModels.Pages;

/// <summary>
/// Coordinates account list, creation, and detail screens under Account Management.
/// </summary>
public sealed class AccountManagementViewModel : ViewModelBase, IAsyncInitializable
{
    private readonly IAccountManagementService _accountService;
    private CancellationTokenSource? _interestRulesRequest;
    private string _screen = "List";
    private string _searchText = string.Empty;
    private string? _selectedStatus;
    private AccountTypeResponse? _selectedFilterType;
    private AccountTypeResponse? _selectedCreateType;
    private AccountResponse? _selectedAccount;
    private string _errorMessage = string.Empty;
    private string _infoMessage = string.Empty;
    private string _openingBalance = "0";
    private string _holderNrc1 = string.Empty;
    private string _holderNrc2 = string.Empty;
    private string _ownershipPercentage1 = "100";
    private string _ownershipPercentage2 = "0";
    private string _signingRule = string.Empty;
    private bool _isSharedAccount;
    private long? _payoutAccountId;
    private InterestRateRuleResponse? _selectedInterestRateRule;
    private string _renewalInstruction = "AtMaturity";
    private bool _calculateFromCurrent;
    private bool _isBusy;
    private bool _hasMore;
    private string? _nextCursor;
    private int _pageNumber = 1;
    private readonly List<string?> _pageCursors = [null];
    private int _createStep = 1;
    private int _maxCreateStepReached = 1;
    private CustomerLookupResponse? _holder1;
    private CustomerLookupResponse? _holder2;
    private bool _showCustomerRegistrationForm;
    private string _registrationFullName = string.Empty;
    private string _registrationNrc = string.Empty;
    private string _registrationDateOfBirth = string.Empty;
    private string _registrationPhone = string.Empty;
    private string _registrationEmail = string.Empty;
    private CustomerType _registrationCustomerType = CustomerType.Citizen;
    private bool _showStatusDialog;
    private string? _statusToApply;
    private string _statusReason = string.Empty;
    private AccountOpeningOptionsResponse? _openingOptions;
    private object? _currentAccountPage;
    private bool _isLoadingTransactions;
    private bool _isLoadingStatusHistory;
    private bool _isLoadingInterestAccruals;
    private string? _transactionsErrorMessage;
    private string? _statusHistoryErrorMessage;
    private string? _interestAccrualsErrorMessage;

    public AccountManagementViewModel(IAccountManagementService accountService)
    {
        _accountService = accountService;
        ListPage = new AccountListPageViewModel(this);
        CreatePage = new AccountCreatePageViewModel(this);
        DetailPage = new AccountDetailPageViewModel(this);
        _currentAccountPage = ListPage;
        SearchAccountsCommand = new AsyncRelayCommand(() => LoadAccountsAsync(false));
        LoadMoreCommand = new AsyncRelayCommand(() => LoadAccountsAsync(true), () => HasMore && !IsBusy);
        PreviousPageCommand = new AsyncRelayCommand(() => ChangePageAsync(-1), () => PageNumber > 1 && !IsBusy);
        NextPageCommand = new AsyncRelayCommand(() => ChangePageAsync(1), () => HasMore && !IsBusy);
        OpenCreateCommand = new RelayCommand(_ => ShowCreate());
        BackToListCommand = new RelayCommand(_ => ShowList());
        OpenAccountCommand = new AsyncRelayCommand(
            parameter => OpenAccountAsync(parameter),
            parameter => parameter is AccountSummaryResponse && !IsBusy);
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync, () => !IsBusy);
        ContinueCreateStepCommand = new AsyncRelayCommand(ContinueCreateStepAsync);
        BackCreateStepCommand = new RelayCommand(_ => MoveToCreateStep(Math.Max(1, CreateStep - 1), "Previous step button"));
        GoToCreateStepCommand = new RelayCommand(NavigateToCreateStep, CanNavigateToCreateStep);
        AddRefererInputCommand = new RelayCommand(_ => RefererInputs.Add(new AccountRefererInputViewModel()));
        RemoveRefererInputCommand = new RelayCommand(RemoveRefererInput);
        LookupCustomersCommand = new AsyncRelayCommand(LookupCustomersAsync);
        CreateCustomerCommand = new AsyncRelayCommand(CreateCustomerAsync, () => !IsBusy);
        OpenStatusDialogCommand = new AsyncRelayCommand(OpenStatusDialogAsync, parameter => !IsBusy && IsStatusActionAvailable(parameter));
        CancelStatusDialogCommand = new RelayCommand(_ => ShowStatusDialog = false);
        ApplyStatusCommand = new AsyncRelayCommand(ApplyStatusAsync);
        SuspendAccountCommand = new AsyncRelayCommand(SuspendAccountAsync, parameter => !IsBusy && parameter is AccountSummaryResponse summary && summary.Status is "Active" or "Dormant");
    }

    public string PageTitle => "Account Management";
    public string PageDescription => "Open accounts and view account records";
    public ObservableCollection<AccountSummaryResponse> Accounts { get; } = [];
    public ObservableCollection<AccountTypeResponse> AccountTypes { get; } = [];
    public ObservableCollection<AccountDocumentInputViewModel> RequiredDocumentInputs { get; } = [];
    public IReadOnlyList<string> StatusOptions { get; } = ["All statuses", "Active", "Dormant", "Suspended", "Closed", "Frozen"];
    public IReadOnlyList<string> RenewalInstructions { get; } = ["NoRenewal", "PrincipalOnly", "PrincipalAndInterest"];

    public AsyncRelayCommand SearchAccountsCommand { get; }
    public AsyncRelayCommand LoadMoreCommand { get; }
    public AsyncRelayCommand PreviousPageCommand { get; }
    public AsyncRelayCommand NextPageCommand { get; }
    public AsyncRelayCommand ContinueCreateStepCommand { get; }
    public RelayCommand BackCreateStepCommand { get; }
    public AsyncRelayCommand LookupCustomersCommand { get; }
    public AsyncRelayCommand CreateCustomerCommand { get; }
    public AsyncRelayCommand OpenStatusDialogCommand { get; }
    public RelayCommand CancelStatusDialogCommand { get; }
    public AsyncRelayCommand ApplyStatusCommand { get; }
    public AsyncRelayCommand SuspendAccountCommand { get; }
    public RelayCommand OpenCreateCommand { get; }
    public RelayCommand BackToListCommand { get; }
    public AsyncRelayCommand OpenAccountCommand { get; }
    public AsyncRelayCommand CreateAccountCommand { get; }
    public RelayCommand AddRefererInputCommand { get; }
    public RelayCommand RemoveRefererInputCommand { get; }

    public string Screen { get => _screen; private set => SetProperty(ref _screen, value); }
    public bool IsListScreen => Screen == "List";
    public bool IsCreateScreen => Screen == "Create";
    public bool IsDetailScreen => Screen == "Detail";
    public int CreateStep { get => _createStep; private set { if (SetProperty(ref _createStep, value)) { OnPropertyChanged(nameof(CreateStepTitle)); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); OnPropertyChanged(nameof(IsStep4)); OnPropertyChanged(nameof(IsStep5)); OnPropertyChanged(nameof(IsNotStep5)); NotifyStepperState(); } } }
    public int MaxCreateStepReached
    {
        get => _maxCreateStepReached;
        private set
        {
            if (SetProperty(ref _maxCreateStepReached, value))
            {
                NotifyStepperState();
                GoToCreateStepCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public string Step1Label => GetStepperLabel(1, "Choose Ownership");
    public string Step2Label => GetStepperLabel(2, "Customer Select");
    public string Step3Label => GetStepperLabel(3, "Account Type");
    public string Step4Label => GetStepperLabel(4, "Documents & Settings");
    public string Step5Label => GetStepperLabel(5, "Create Account");
    public System.Windows.Media.Brush Step1Foreground => GetStepperForeground(1);
    public System.Windows.Media.Brush Step2Foreground => GetStepperForeground(2);
    public System.Windows.Media.Brush Step3Foreground => GetStepperForeground(3);
    public System.Windows.Media.Brush Step4Foreground => GetStepperForeground(4);
    public System.Windows.Media.Brush Step5Foreground => GetStepperForeground(5);
    public System.Windows.Media.Brush Step1Background => GetStepperBackground(1);
    public System.Windows.Media.Brush Step2Background => GetStepperBackground(2);
    public System.Windows.Media.Brush Step3Background => GetStepperBackground(3);
    public System.Windows.Media.Brush Step4Background => GetStepperBackground(4);
    public System.Windows.Media.Brush Step5Background => GetStepperBackground(5);
    public string CreateStepTitle => CreateStep switch { 1 => "Ownership", 2 => "Customer holders", 3 => "Account type", 4 => "Documents and settings", _ => "Review and create" };
    public bool IsStep1 => CreateStep == 1;
    public bool IsStep2 => CreateStep == 2;
    public bool IsStep3 => CreateStep == 3;
    public bool IsStep4 => CreateStep == 4;
    public bool IsStep5 => CreateStep == 5;
    public bool IsNotStep5 => CreateStep != 5;
    public int PageNumber { get => _pageNumber; private set { if (SetProperty(ref _pageNumber, value)) { PreviousPageCommand.RaiseCanExecuteChanged(); OnPropertyChanged(nameof(PageLabel)); } } }
    public string PageLabel => $"Page {PageNumber}";
    public CustomerLookupResponse? Holder1 { get => _holder1; private set { if (SetProperty(ref _holder1, value)) UpdateRequiredRefererInputs(); } }
    public CustomerLookupResponse? Holder2 { get => _holder2; private set { if (SetProperty(ref _holder2, value)) UpdateRequiredRefererInputs(); } }
    public bool ShowCustomerRegistrationForm { get => _showCustomerRegistrationForm; private set => SetProperty(ref _showCustomerRegistrationForm, value); }
    public string RegistrationFullName { get => _registrationFullName; set => SetProperty(ref _registrationFullName, value); }
    public string RegistrationNrc { get => _registrationNrc; private set => SetProperty(ref _registrationNrc, value); }
    public string RegistrationDateOfBirth { get => _registrationDateOfBirth; set => SetProperty(ref _registrationDateOfBirth, value); }
    public string RegistrationPhone { get => _registrationPhone; set => SetProperty(ref _registrationPhone, value); }
    public string RegistrationEmail { get => _registrationEmail; set => SetProperty(ref _registrationEmail, value); }
    public CustomerType RegistrationCustomerType { get => _registrationCustomerType; set => SetProperty(ref _registrationCustomerType, value); }
    public IReadOnlyList<CustomerType> CustomerTypes { get; } = [CustomerType.Citizen, CustomerType.Foreigner];
    public ObservableCollection<AccountTypeResponse> EligibleAccountTypes { get; } = [];
    public ObservableCollection<OwnedAccountOptionResponse> OwnedAccounts { get; } = [];
    public ObservableCollection<AccountTypeRequiredDocumentResponse> RequiredDocuments { get; } = [];
    public ObservableCollection<AccountTransactionDetailResponse> AccountTransactions { get; } = [];
    public ObservableCollection<AccountStatusHistoryResponse> AccountStatusHistory { get; } = [];
    public ObservableCollection<InterestAccrualResponse> InterestAccruals { get; } = [];
    public bool IsLoadingTransactions { get => _isLoadingTransactions; private set { if (SetProperty(ref _isLoadingTransactions, value)) { OnPropertyChanged(nameof(TransactionsSectionMessage)); OnPropertyChanged(nameof(ShowTransactionsSectionMessage)); } } }
    public bool IsLoadingStatusHistory { get => _isLoadingStatusHistory; private set { if (SetProperty(ref _isLoadingStatusHistory, value)) { OnPropertyChanged(nameof(StatusHistorySectionMessage)); OnPropertyChanged(nameof(ShowStatusHistorySectionMessage)); } } }
    public bool IsLoadingInterestAccruals { get => _isLoadingInterestAccruals; private set { if (SetProperty(ref _isLoadingInterestAccruals, value)) { OnPropertyChanged(nameof(InterestAccrualsSectionMessage)); OnPropertyChanged(nameof(ShowInterestAccrualsSectionMessage)); } } }
    public string? TransactionsErrorMessage { get => _transactionsErrorMessage; private set { if (SetProperty(ref _transactionsErrorMessage, value)) { OnPropertyChanged(nameof(TransactionsSectionMessage)); OnPropertyChanged(nameof(ShowTransactionsSectionMessage)); } } }
    public string? StatusHistoryErrorMessage { get => _statusHistoryErrorMessage; private set { if (SetProperty(ref _statusHistoryErrorMessage, value)) { OnPropertyChanged(nameof(StatusHistorySectionMessage)); OnPropertyChanged(nameof(ShowStatusHistorySectionMessage)); } } }
    public string? InterestAccrualsErrorMessage { get => _interestAccrualsErrorMessage; private set { if (SetProperty(ref _interestAccrualsErrorMessage, value)) { OnPropertyChanged(nameof(InterestAccrualsSectionMessage)); OnPropertyChanged(nameof(ShowInterestAccrualsSectionMessage)); } } }
    public string TransactionsSectionMessage => IsLoadingTransactions ? "Loading transactions…" : TransactionsErrorMessage ?? (AccountTransactions.Count == 0 ? "No transactions have been recorded for this account yet." : string.Empty);
    public string StatusHistorySectionMessage => IsLoadingStatusHistory ? "Loading status history…" : StatusHistoryErrorMessage ?? (AccountStatusHistory.Count == 0 ? "No status changes have been recorded for this account yet." : string.Empty);
    public string InterestAccrualsSectionMessage => IsLoadingInterestAccruals ? "Loading interest accruals…" : InterestAccrualsErrorMessage ?? (InterestAccruals.Count == 0 ? "No interest accruals have been recorded for this account yet." : string.Empty);
    public bool ShowTransactionsSectionMessage => IsLoadingTransactions || TransactionsErrorMessage is not null || AccountTransactions.Count == 0;
    public bool ShowStatusHistorySectionMessage => IsLoadingStatusHistory || StatusHistoryErrorMessage is not null || AccountStatusHistory.Count == 0;
    public bool ShowInterestAccrualsSectionMessage => IsLoadingInterestAccruals || InterestAccrualsErrorMessage is not null || InterestAccruals.Count == 0;
    public ObservableCollection<AccountRefererInputViewModel> RefererInputs { get; } = [];
    public int RequiredRefererCount => SelectedCreateType is null ? 0 :
        CountRequiredReferers(Holder1) + CountRequiredReferers(Holder2);
    public IReadOnlyList<string> StatusChangeOptions => SelectedAccount?.Status switch
    {
        "Active" => ["Frozen"],
        "Dormant" => ["Active", "Frozen"],
        "Suspended" or "Frozen" => ["Active"],
        _ => []
    };
    public bool ShowStatusDialog { get => _showStatusDialog; private set => SetProperty(ref _showStatusDialog, value); }
    public string? StatusToApply { get => _statusToApply; set => SetProperty(ref _statusToApply, value); }
    public string StatusReason { get => _statusReason; set => SetProperty(ref _statusReason, value); }
    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public string? SelectedStatus { get => _selectedStatus; set => SetProperty(ref _selectedStatus, value); }
    public AccountTypeResponse? SelectedFilterType { get => _selectedFilterType; set => SetProperty(ref _selectedFilterType, value); }
    public AccountTypeResponse? SelectedCreateType
    {
        get => _selectedCreateType;
        set
        {
            if (SetProperty(ref _selectedCreateType, value))
            {
                OnPropertyChanged(nameof(IsFixedDeposit));
                OnPropertyChanged(nameof(RequiredRefererCount));
                UpdateRequiredRefererInputs();
                LoadRequiredDocumentInputs(value?.Id);
                _ = LoadInterestRateRulesAsync(value?.Id);
            }
        }
    }
    public bool IsFixedDeposit => SelectedCreateType?.IsFixedDeposit == true;
    public ObservableCollection<InterestRateRuleResponse> InterestRateRules { get; } = [];
    public InterestRateRuleResponse? SelectedInterestRateRule
    {
        get => _selectedInterestRateRule;
        set => SetProperty(ref _selectedInterestRateRule, value);
    }

    public AccountListPageViewModel ListPage { get; }
    public AccountCreatePageViewModel CreatePage { get; }
    public AccountDetailPageViewModel DetailPage { get; }
    public object? CurrentAccountPage { get => _currentAccountPage; private set => SetProperty(ref _currentAccountPage, value); }
    public RelayCommand GoToCreateStepCommand { get; }

    // Changes the embedded account page while retaining this workflow's list and draft state.
    public void NavigateToList() => CurrentAccountPage = ListPage;
    public void NavigateToCreate() => CurrentAccountPage = CreatePage;
    public void NavigateToDetail() => CurrentAccountPage = DetailPage;
    public void RecoverToListAfterUnexpectedError(string message)
    {
        ErrorMessage = message;
        NavigateToList();
        Screen = "List";
        NotifyScreenChanged();
    }
    public void NavigateToCreateStep(object? step)
    {
        if (!int.TryParse(step?.ToString(), out var value) || value is < 1 or > 5)
        {
            AppLog.WriteInformation($"Account creation stepper ignored invalid step parameter '{step ?? "null"}'.");
            return;
        }

        if (value > MaxCreateStepReached)
        {
            AppLog.WriteInformation($"Account creation stepper blocked navigation from step {CreateStep} to locked step {value}; highest unlocked step is {MaxCreateStepReached}.");
            return;
        }

        MoveToCreateStep(value, "Stepper click");
    }

    private bool CanNavigateToCreateStep(object? step) =>
        int.TryParse(step?.ToString(), out var value) && value is >= 1 and <= 5 && value <= MaxCreateStepReached;

    private void MoveToCreateStep(int step, string source)
    {
        var previous = CreateStep;
        CreateStep = step;
        AppLog.WriteInformation($"Account creation step changed. Source={source}, From={previous}, To={CreateStep}, HighestUnlocked={MaxCreateStepReached}.");
    }

    private void AdvanceCreateStep(int nextStep)
    {
        MaxCreateStepReached = Math.Max(MaxCreateStepReached, nextStep);
        MoveToCreateStep(nextStep, "Continue after validation");
    }

    private static string GetStepperLabel(int step, string title) => $"{step}. {title}";

    private System.Windows.Media.Brush GetStepperForeground(int step)
    {
        var resourceKey = step < MaxCreateStepReached
            ? "SuccessBrush"
            : step == CreateStep
                ? "PrimaryBrush"
                : "MutedForegroundBrush";
        return System.Windows.Application.Current?.TryFindResource(resourceKey) as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Gray;
    }

    private System.Windows.Media.Brush GetStepperBackground(int step) =>
        step < MaxCreateStepReached
            ? System.Windows.Application.Current?.TryFindResource("SuccessSoftBrush") as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.Transparent
            : System.Windows.Media.Brushes.Transparent;

    private void NotifyStepperState()
    {
        OnPropertyChanged(nameof(Step1Label));
        OnPropertyChanged(nameof(Step2Label));
        OnPropertyChanged(nameof(Step3Label));
        OnPropertyChanged(nameof(Step4Label));
        OnPropertyChanged(nameof(Step5Label));
        OnPropertyChanged(nameof(Step1Foreground));
        OnPropertyChanged(nameof(Step2Foreground));
        OnPropertyChanged(nameof(Step3Foreground));
        OnPropertyChanged(nameof(Step4Foreground));
        OnPropertyChanged(nameof(Step5Foreground));
        OnPropertyChanged(nameof(Step1Background));
        OnPropertyChanged(nameof(Step2Background));
        OnPropertyChanged(nameof(Step3Background));
        OnPropertyChanged(nameof(Step4Background));
        OnPropertyChanged(nameof(Step5Background));
    }
    public bool IsLoadingInterestRateRules { get; private set; }
    public bool HasNoInterestRateRules => SelectedCreateType is not null && !IsLoadingInterestRateRules && InterestRateRules.Count == 0;
    public AccountResponse? SelectedAccount { get => _selectedAccount; private set { if (SetProperty(ref _selectedAccount, value)) OnPropertyChanged(nameof(StatusChangeOptions)); } }
    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(ToastMessage));
                OnPropertyChanged(nameof(HasErrorNotification));
            }
        }
    }
    public string InfoMessage
    {
        get => _infoMessage;
        private set
        {
            if (SetProperty(ref _infoMessage, value))
            {
                OnPropertyChanged(nameof(ToastMessage));
            }
        }
    }
    public string ToastMessage => !string.IsNullOrWhiteSpace(ErrorMessage) ? ErrorMessage : InfoMessage;
    public bool HasErrorNotification => !string.IsNullOrWhiteSpace(ErrorMessage);
    public string OpeningBalance { get => _openingBalance; set => SetProperty(ref _openingBalance, value); }
    public string HolderNrc1 { get => _holderNrc1; set => SetProperty(ref _holderNrc1, value); }
    public string HolderNrc2 { get => _holderNrc2; set => SetProperty(ref _holderNrc2, value); }
    public string OwnershipPercentage1 { get => _ownershipPercentage1; set => SetProperty(ref _ownershipPercentage1, value); }
    public string OwnershipPercentage2 { get => _ownershipPercentage2; set => SetProperty(ref _ownershipPercentage2, value); }
    public string SigningRule { get => _signingRule; set => SetProperty(ref _signingRule, value); }
    public bool IsSharedAccount { get => _isSharedAccount; set { if (SetProperty(ref _isSharedAccount, value)) OnPropertyChanged(nameof(IsIndividualAccount)); } }
    public bool IsIndividualAccount { get => !IsSharedAccount; set { if (value) IsSharedAccount = false; } }
    public long? PayoutAccountId { get => _payoutAccountId; set => SetProperty(ref _payoutAccountId, value); }
    public string RenewalInstruction { get => _renewalInstruction; set => SetProperty(ref _renewalInstruction, value); }
    public bool CalculateFromCurrent { get => _calculateFromCurrent; set => SetProperty(ref _calculateFromCurrent, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                LoadMoreCommand.RaiseCanExecuteChanged();
                CreateAccountCommand.RaiseCanExecuteChanged();
                CreateCustomerCommand.RaiseCanExecuteChanged();
                OpenAccountCommand.RaiseCanExecuteChanged();
                PreviousPageCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
                OpenStatusDialogCommand.RaiseCanExecuteChanged();
                SuspendAccountCommand.RaiseCanExecuteChanged();
            }
        }
    }
    public bool HasMore
    {
        get => _hasMore;
        private set
        {
            if (SetProperty(ref _hasMore, value))
            {
                LoadMoreCommand.RaiseCanExecuteChanged();
                NextPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await RunBusyAsync(async () =>
        {
            var types = await _accountService.GetAccountTypesAsync(cancellationToken);
            AccountTypes.Clear();
            foreach (var type in types)
            {
                AccountTypes.Add(type);
            }
            await LoadAccountsCoreAsync(false, cancellationToken);
            if (IsDetailScreen && SelectedAccount is not null)
            {
                await LoadDetailCollectionsAsync(SelectedAccount.Id);
            }
        }, "Could not initialize account management.", cancellationToken);
    }

    private async Task LoadAccountsAsync(bool append)
    {
        if (!append)
        {
            PageNumber = 1;
            _pageCursors.Clear();
            _pageCursors.Add(null);
        }
        await RunBusyAsync(() => LoadAccountsCoreAsync(append, CancellationToken.None), "Could not load accounts.");
    }

    private async Task LoadAccountsCoreAsync(bool append, CancellationToken cancellationToken)
    {
        ErrorMessage = string.Empty;
        if (!append)
        {
            _nextCursor = null;
            HasMore = false;
        }

        var status = SelectedStatus is "All statuses" ? null : SelectedStatus;
        var page = await _accountService.GetAccountsAsync(
            new AccountListCriteria(SearchText, SelectedFilterType?.Id, status, append ? _nextCursor : _pageCursors[PageNumber - 1], 10),
            cancellationToken);

        if (!append)
        {
            Accounts.Clear();
        }

        foreach (var account in page.Items)
        {
            Accounts.Add(account);
        }

        _nextCursor = page.NextCursor;
        HasMore = page.HasMore;
        if (page.HasMore && _pageCursors.Count == PageNumber) _pageCursors.Add(page.NextCursor);
        InfoMessage = Accounts.Count == 0 ? "No accounts match these filters." : $"{PageLabel} · {Accounts.Count} account(s)";
        NextPageCommand.RaiseCanExecuteChanged();
    }

    private async Task ChangePageAsync(int direction)
    {
        if (direction < 0 && PageNumber > 1) PageNumber--;
        else if (direction > 0 && HasMore) PageNumber++;
        await RunBusyAsync(() => LoadAccountsCoreAsync(false, CancellationToken.None), "Could not change account page.");
    }

    private void ShowCreate()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        if (CreateStep == 0) CreateStep = 1;
        NavigateToCreate();
        Screen = "Create";
        NotifyScreenChanged();
    }

    private void ShowList()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        NavigateToList();
        Screen = "List";
        NotifyScreenChanged();
    }

    private async Task OpenAccountAsync(object? parameter)
    {
        if (parameter is not AccountSummaryResponse summary)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            ErrorMessage = string.Empty;
            // The list uses InfoMessage for pagination status; don't carry that text into the detail toast.
            InfoMessage = string.Empty;
            SelectedAccount = await _accountService.GetAccountAsync(summary.Id, CancellationToken.None);
            ClearDetailCollections();
            Screen = "Detail";
            NavigateToDetail();
            NotifyScreenChanged();
            await LoadDetailCollectionsAsync(summary.Id);
        }, "Could not open the account details.");
    }

    private async Task CreateAccountAsync()
    {
        ErrorMessage = string.Empty;
        if (SelectedCreateType is null)
        {
            ShowError("Choose an account type.");
            return;
        }

        if (string.IsNullOrWhiteSpace(HolderNrc1))
        {
            ShowError("Enter the primary holder's NRC number.");
            return;
        }

        if (!decimal.TryParse(OpeningBalance, NumberStyles.Number, CultureInfo.InvariantCulture, out var openingBalance))
        {
            ShowError("Enter a valid opening balance.");
            return;
        }

        if (openingBalance < SelectedCreateType.MinimumOpeningBalance)
        {
            ShowError($"The minimum opening balance for this product is {SelectedCreateType.MinimumOpeningBalance:N2}.");
            return;
        }

        if (IsSharedAccount && (string.IsNullOrWhiteSpace(HolderNrc2) ||
            !decimal.TryParse(OwnershipPercentage1, NumberStyles.Number, CultureInfo.InvariantCulture, out _) ||
            !decimal.TryParse(OwnershipPercentage2, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
        {
            ShowError("Enter both holder NRCs and valid ownership percentages for a shared account.");
            return;
        }

        if (IsFixedDeposit && (!PayoutAccountId.HasValue || SelectedInterestRateRule is null))
        {
            ShowError("Choose a payout account and an interest rate rule for this fixed deposit.");
            return;
        }

        var request = new CreateAccountCommand(
            SelectedCreateType.Id,
            openingBalance,
            IsSharedAccount,
            NullIfBlank(HolderNrc1),
            IsSharedAccount ? NullIfBlank(HolderNrc2) : null,
            IsSharedAccount ? TryOptionalDecimal(OwnershipPercentage1) : null,
            IsSharedAccount ? TryOptionalDecimal(OwnershipPercentage2) : null,
            NullIfBlank(SigningRule),
            RequiredDocumentInputs
                .Select(input => new AccountDocumentUpload(input.DocumentType, null, input.FilePath!))
                .ToArray(),
            IsFixedDeposit ? PayoutAccountId : null,
            IsFixedDeposit ? SelectedInterestRateRule?.Id : null,
            IsFixedDeposit ? RenewalInstruction : null,
            IsFixedDeposit ? CalculateFromCurrent : null,
            RefererInputs.Select(input => input.Nrc.Trim()).Where(nrc => nrc.Length > 0).ToArray());

        await RunBusyAsync(async () =>
        {
            SelectedAccount = await _accountService.CreateAccountAsync(request, CancellationToken.None);
            Screen = "Detail";
            NavigateToDetail();
            InfoMessage = "Account created successfully.";
            NotifyScreenChanged();
            try
            {
                await LoadDetailCollectionsAsync(SelectedAccount.Id);
            }
            catch (Exception exception)
            {
                LogException("Load account details after account creation", exception);
                InfoMessage = "Account created successfully.";
                ShowError("The account was created, but some detail records could not be loaded.");
            }
            ResetCreateForm();
            CreateStep = 1;
        }, "Could not create the account.", returnToListOnUnexpectedError: true);
    }

    private void ResetCreateForm()
    {
        Holder1 = null;
        Holder2 = null;
        ShowCustomerRegistrationForm = false;
        RegistrationFullName = string.Empty;
        RegistrationNrc = string.Empty;
        RegistrationDateOfBirth = string.Empty;
        RegistrationPhone = string.Empty;
        RegistrationEmail = string.Empty;
        RegistrationCustomerType = CustomerType.Citizen;
        _openingOptions = null;
        EligibleAccountTypes.Clear();
        OwnedAccounts.Clear();
        RequiredDocuments.Clear();
        SelectedCreateType = null;
        OpeningBalance = "0";
        HolderNrc1 = string.Empty;
        HolderNrc2 = string.Empty;
        OwnershipPercentage1 = "100";
        OwnershipPercentage2 = "0";
        SigningRule = string.Empty;
        IsSharedAccount = false;
        PayoutAccountId = null;
        SelectedInterestRateRule = null;
        RenewalInstruction = RenewalInstructions[0];
        CalculateFromCurrent = false;
        RequiredDocumentInputs.Clear();
        RefererInputs.Clear();
        MaxCreateStepReached = 1;
    }

    private async Task RunBusyAsync(
        Func<Task> action,
        string fallbackMessage,
        CancellationToken cancellationToken = default,
        bool returnToListOnUnexpectedError = false)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await action();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A caller-requested cancellation is expected and should not be presented as a failure.
        }
        catch (AppException exception)
        {
            ReportError(fallbackMessage, exception, exception.Message);
        }
        catch (IOException exception)
        {
            ReportError(fallbackMessage, exception, exception.Message);
        }
        catch (Exception exception)
        {
            ReportError(fallbackMessage, exception, fallbackMessage);
            if (returnToListOnUnexpectedError)
            {
                RecoverToListAfterUnexpectedError(fallbackMessage);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ContinueCreateStepAsync()
    {
        ErrorMessage = string.Empty;
        if (CreateStep == 1)
        {
            AdvanceCreateStep(2);
            return;
        }
        if (CreateStep == 2)
        {
            if (Holder1 is null || (IsSharedAccount && Holder2 is null))
            {
                ShowError("Look up each account holder by NRC before continuing.");
                return;
            }
            await RunBusyAsync(async () =>
            {
                _openingOptions = await _accountService.GetAccountOpeningOptionsAsync(HolderNrc1, IsSharedAccount ? HolderNrc2 : null, CancellationToken.None);
                ReplaceItems(EligibleAccountTypes, _openingOptions.AccountTypes);
                ReplaceItems(RequiredDocuments, _openingOptions.RequiredDocuments);
                ReplaceItems(OwnedAccounts, _openingOptions.OwnedAccounts);
                AdvanceCreateStep(3);
            }, "Could not load account-opening options.");
            return;
        }
        if (CreateStep == 3 && SelectedCreateType is null)
        {
            ShowError("Choose an eligible account type.");
            return;
        }
        if (CreateStep == 4)
        {
            var missing = RequiredDocumentInputs
                .Where(input => string.IsNullOrWhiteSpace(input.FilePath) || !File.Exists(input.FilePath))
                .Select(input => input.DocumentType)
                .ToArray();
            if (missing.Length > 0) { ShowError($"Upload required documents: {string.Join(", ", missing)}."); return; }
        }
        AdvanceCreateStep(Math.Min(5, CreateStep + 1));
    }

    // Rebuilds the file inputs whenever the selected product changes so uploads always match its requirements.
    private void LoadRequiredDocumentInputs(long? accountTypeId)
    {
        RequiredDocumentInputs.Clear();
        if (!accountTypeId.HasValue)
        {
            return;
        }

        foreach (var requirement in RequiredDocuments
                     .Where(item => item.AccountTypeId == accountTypeId.Value)
                     .OrderBy(item => item.DocumentType, StringComparer.OrdinalIgnoreCase))
        {
            RequiredDocumentInputs.Add(new AccountDocumentInputViewModel(requirement.DocumentType));
        }
    }

    // Adds rows up to the selected holders' required minimum without discarding entered extra rows.
    private void UpdateRequiredRefererInputs()
    {
        OnPropertyChanged(nameof(RequiredRefererCount));
        while (RefererInputs.Count < RequiredRefererCount)
            RefererInputs.Add(new AccountRefererInputViewModel());
    }

    private int CountRequiredReferers(CustomerLookupResponse? customer)
    {
        if (customer is null || SelectedCreateType is null) return 0;
        return customer.CustomerType switch
        {
            CustomerType.Citizen => SelectedCreateType.CitizenRequiredRefer,
            CustomerType.Foreigner => SelectedCreateType.ForeignRequiredRefer,
            _ => 0
        };
    }

    private void RemoveRefererInput(object? parameter)
    {
        if (parameter is AccountRefererInputViewModel input && RefererInputs.Count > RequiredRefererCount)
            RefererInputs.Remove(input);
    }

    private async Task LookupCustomersAsync()
    {
        ErrorMessage = string.Empty;
        ShowCustomerRegistrationForm = false;
        RegistrationNrc = string.Empty;
        Holder1 = null;
        Holder2 = null;
        if (string.IsNullOrWhiteSpace(HolderNrc1) || (IsSharedAccount && string.IsNullOrWhiteSpace(HolderNrc2)))
        {
            ShowError("Enter NRC numbers for all account holders.");
            return;
        }
        string? missingCustomerNrc = null;
        await RunBusyAsync(async () =>
        {
            try
            {
                Holder1 = await _accountService.GetCustomerByNrcAsync(HolderNrc1, CancellationToken.None);
            }
            catch (ApiException exception) when (exception.Code == MessageCode.CustomerNotFound)
            {
                missingCustomerNrc = HolderNrc1.Trim();
            }

            if (IsSharedAccount)
            {
                try
                {
                    Holder2 = await _accountService.GetCustomerByNrcAsync(HolderNrc2, CancellationToken.None);
                }
                catch (ApiException exception) when (exception.Code == MessageCode.CustomerNotFound)
                {
                    missingCustomerNrc ??= HolderNrc2.Trim();
                }
            }
        }, "Could not look up the account holders.");
        if (missingCustomerNrc is not null)
        {
            ShowCustomerRegistrationForm = true;
            RegistrationNrc = missingCustomerNrc;
            ShowError("A customer was not found. Register the customer to continue.");
        }
    }

    /// <summary>Creates the missing customer through the backend and selects the persisted profile as a holder.</summary>
    private async Task CreateCustomerAsync()
    {
        ErrorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(RegistrationFullName) ||
            !DateOnly.TryParse(RegistrationDateOfBirth, CultureInfo.InvariantCulture, out var dateOfBirth) ||
            string.IsNullOrWhiteSpace(RegistrationNrc))
        {
            ShowError("Enter the customer's full name and a valid date of birth.");
            return;
        }

        await RunBusyAsync(async () =>
        {
            var customer = await _accountService.CreateCustomerAsync(
                new CreateCustomerRequest(
                    RegistrationFullName.Trim(),
                    dateOfBirth,
                    RegistrationNrc,
                    NullIfBlank(RegistrationPhone),
                    NullIfBlank(RegistrationEmail),
                    RegistrationCustomerType),
                CancellationToken.None);

            var registeredPrimaryHolder = string.Equals(
                RegistrationNrc,
                HolderNrc1.Trim(),
                StringComparison.OrdinalIgnoreCase);
            if (registeredPrimaryHolder)
            {
                Holder1 = customer;
            }
            else
            {
                Holder2 = customer;
            }

            ErrorMessage = string.Empty;
            var nextMissingNrc = IsSharedAccount
                ? Holder1 is null ? HolderNrc1.Trim() : Holder2 is null ? HolderNrc2.Trim() : null
                : null;

            if (!string.IsNullOrWhiteSpace(nextMissingNrc))
            {
                RegistrationFullName = string.Empty;
                RegistrationDateOfBirth = string.Empty;
                RegistrationPhone = string.Empty;
                RegistrationEmail = string.Empty;
                RegistrationCustomerType = CustomerType.Citizen;
                RegistrationNrc = nextMissingNrc;
                ShowCustomerRegistrationForm = true;
                InfoMessage = $"Customer {customer.FullName} was created. Register the other account holder to continue.";
            }
            else
            {
                ShowCustomerRegistrationForm = false;
                InfoMessage = $"Customer {customer.FullName} ({customer.CustomerNo}) was created and selected as an account holder.";
            }
        }, "Could not create the customer.");
    }

    private async Task OpenStatusDialogAsync(object? parameter)
    {
        if (parameter is not AccountSummaryResponse summary) return;
        await RunBusyAsync(async () =>
        {
            SelectedAccount = await _accountService.GetAccountAsync(summary.Id, CancellationToken.None);
            StatusToApply = StatusChangeOptions.FirstOrDefault();
            StatusReason = string.Empty;
            ShowStatusDialog = true;
        }, "Could not load the account for a status change.");
    }

    // Keeps list actions aligned with the status transitions supported by the server.
    private bool IsStatusActionAvailable(object? parameter)
    {
        return parameter is AccountSummaryResponse summary && summary.Status is "Active" or "Dormant" or "Suspended" or "Frozen";
    }

    private async Task ApplyStatusAsync()
    {
        if (SelectedAccount is null || string.IsNullOrWhiteSpace(StatusToApply)) return;
        await RunBusyAsync(async () =>
        {
            var updated = await _accountService.UpdateAccountStatusAsync(SelectedAccount.Id, StatusToApply, NullIfBlank(StatusReason), SelectedAccount.Version, CancellationToken.None);
            var row = Accounts.FirstOrDefault(item => item.Id == updated.Id);
            if (row is not null) { var index = Accounts.IndexOf(row); Accounts[index] = row with { Status = updated.Status }; }
            SelectedAccount = updated;
            ShowStatusDialog = false;
            InfoMessage = "Account status updated.";
        }, "Could not update the account status.");
    }

    private async Task SuspendAccountAsync(object? parameter)
    {
        if (parameter is not AccountSummaryResponse summary) return;
        await RunBusyAsync(async () =>
        {
            var current = await _accountService.GetAccountAsync(summary.Id, CancellationToken.None);
            var updated = await _accountService.UpdateAccountStatusAsync(summary.Id, "Suspended", "Suspended from account list.", current.Version, CancellationToken.None);
            var row = Accounts.FirstOrDefault(item => item.Id == updated.Id);
            if (row is not null) { var index = Accounts.IndexOf(row); Accounts[index] = row with { Status = updated.Status }; }
            InfoMessage = "Account suspended.";
        }, "Could not suspend the account.");
    }

    private async Task LoadDetailCollectionsAsync(long accountId)
    {
        await Task.WhenAll(
            LoadDetailSectionAsync("transactions", () => _accountService.GetAccountTransactionsAsync(accountId, CancellationToken.None), AccountTransactions,
                value => IsLoadingTransactions = value, value => TransactionsErrorMessage = value, nameof(TransactionsSectionMessage)),
            LoadDetailSectionAsync("status history", () => _accountService.GetAccountStatusHistoryAsync(accountId, CancellationToken.None), AccountStatusHistory,
                value => IsLoadingStatusHistory = value, value => StatusHistoryErrorMessage = value, nameof(StatusHistorySectionMessage)),
            LoadDetailSectionAsync("interest accruals", () => _accountService.GetAccountInterestAccrualsAsync(accountId, CancellationToken.None), InterestAccruals,
                value => IsLoadingInterestAccruals = value, value => InterestAccrualsErrorMessage = value, nameof(InterestAccrualsSectionMessage)));
    }

    private async Task LoadDetailSectionAsync<T>(
        string sectionName,
        Func<Task<IReadOnlyList<T>>> load,
        ObservableCollection<T> target,
        Action<bool> setLoading,
        Action<string?> setError,
        string messageProperty)
    {
        setLoading(true);
        setError(null);
        target.Clear();
        OnPropertyChanged(messageProperty);
        try
        {
            var items = await load();
            ReplaceItems(target, items);
        }
        catch (Exception exception)
        {
            AppLog.WriteError($"{nameof(AccountManagementViewModel)}: Could not load account {sectionName}.", exception);
            setError(exception is AppException
                ? exception.Message
                : $"{sectionName} could not be loaded. You can still view the account details and try again later.");
        }
        finally
        {
            setLoading(false);
            OnPropertyChanged(messageProperty);
            OnPropertyChanged(sectionName switch
            {
                "transactions" => nameof(ShowTransactionsSectionMessage),
                "status history" => nameof(ShowStatusHistorySectionMessage),
                _ => nameof(ShowInterestAccrualsSectionMessage)
            });
        }
    }

    private void ClearDetailCollections()
    {
        AccountTransactions.Clear();
        AccountStatusHistory.Clear();
        InterestAccruals.Clear();
        TransactionsErrorMessage = null;
        StatusHistoryErrorMessage = null;
        InterestAccrualsErrorMessage = null;
        OnPropertyChanged(nameof(TransactionsSectionMessage));
        OnPropertyChanged(nameof(StatusHistorySectionMessage));
        OnPropertyChanged(nameof(InterestAccrualsSectionMessage));
        OnPropertyChanged(nameof(ShowTransactionsSectionMessage));
        OnPropertyChanged(nameof(ShowStatusHistorySectionMessage));
        OnPropertyChanged(nameof(ShowInterestAccrualsSectionMessage));
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var item in values) target.Add(item);
    }

    private async Task LoadInterestRateRulesAsync(long? accountTypeId)
    {
        _interestRulesRequest?.Cancel();
        _interestRulesRequest?.Dispose();
        _interestRulesRequest = null;
        InterestRateRules.Clear();
        SelectedInterestRateRule = null;
        OnPropertyChanged(nameof(HasNoInterestRateRules));
        ErrorMessage = string.Empty;
        if (!accountTypeId.HasValue)
        {
            SetLoadingInterestRateRules(false);
            return;
        }

        var request = new CancellationTokenSource();
        _interestRulesRequest = request;
        SetLoadingInterestRateRules(true);
        try
        {
            var rules = await _accountService.GetInterestRateRulesAsync(accountTypeId.Value, request.Token);
            if (request.IsCancellationRequested || SelectedCreateType?.Id != accountTypeId.Value)
            {
                return;
            }
            foreach (var rule in rules)
            {
                InterestRateRules.Add(rule);
            }
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            // A newer product selection replaced this request.
        }
        catch (Exception exception)
        {
            if (!request.IsCancellationRequested && SelectedCreateType?.Id == accountTypeId.Value)
            {
                var message = exception is AppException or IOException
                    ? exception.Message
                    : "Could not load interest rate rules for this account type.";
                ReportError("Load interest rate rules", exception, message);
            }
        }
        finally
        {
            if (ReferenceEquals(_interestRulesRequest, request))
            {
                _interestRulesRequest = null;
                SetLoadingInterestRateRules(false);
            }
            request.Dispose();
            OnPropertyChanged(nameof(HasNoInterestRateRules));
        }
    }

    private void SetLoadingInterestRateRules(bool value)
    {
        if (IsLoadingInterestRateRules == value)
        {
            return;
        }
        IsLoadingInterestRateRules = value;
        OnPropertyChanged(nameof(IsLoadingInterestRateRules));
        OnPropertyChanged(nameof(HasNoInterestRateRules));
    }

    // Reports user-safe errors while retaining exception details in the debugger output.
    private void ReportError(string operation, Exception exception, string userMessage)
    {
        LogException(operation, exception);
        ShowError(string.IsNullOrWhiteSpace(userMessage)
            ? "The account operation could not be completed. Please try again."
            : userMessage);
    }

    // Recoverable account errors stay inline so users can correct the input or retry in place.
    private void ShowError(string message)
    {
        ErrorMessage = message;
    }

    private static void LogException(string operation, Exception exception)
    {
        AppLog.WriteError($"{nameof(AccountManagementViewModel)}: {operation} failed.", exception);
    }

    private void NotifyScreenChanged()
    {
        OnPropertyChanged(nameof(IsListScreen));
        OnPropertyChanged(nameof(IsCreateScreen));
        OnPropertyChanged(nameof(IsDetailScreen));
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal? TryOptionalDecimal(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
}
