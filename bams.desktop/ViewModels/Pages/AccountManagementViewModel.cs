using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using bams.desktop.Commands;
using bams.desktop.DTOs.Accounts;
using bams.desktop.Exceptions;
using bams.desktop.Services;
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
    private string _payoutAccountId = string.Empty;
    private InterestRateRuleResponse? _selectedInterestRateRule;
    private string _renewalInstruction = "AtMaturity";
    private bool _calculateFromCurrent;
    private bool _isBusy;
    private bool _hasMore;
    private string? _nextCursor;
    private string _selectedDocumentPath = string.Empty;

    public AccountManagementViewModel(IAccountManagementService accountService)
    {
        _accountService = accountService;
        SearchAccountsCommand = new AsyncRelayCommand(() => LoadAccountsAsync(false));
        LoadMoreCommand = new AsyncRelayCommand(() => LoadAccountsAsync(true), () => HasMore && !IsBusy);
        OpenCreateCommand = new RelayCommand(_ => ShowCreate());
        BackToListCommand = new RelayCommand(_ => ShowList());
        OpenAccountCommand = new AsyncRelayCommand(
            parameter => OpenAccountAsync(parameter),
            parameter => parameter is AccountSummaryResponse && !IsBusy);
        CreateAccountCommand = new AsyncRelayCommand(CreateAccountAsync, () => !IsBusy);
        AddDocumentCommand = new RelayCommand(_ => AddSelectedDocument());
        RemoveDocumentCommand = new RelayCommand(RemoveDocument);
    }

    public string PageTitle => "Account Management";
    public string PageDescription => "Open accounts and view account records";
    public ObservableCollection<AccountSummaryResponse> Accounts { get; } = [];
    public ObservableCollection<AccountTypeResponse> AccountTypes { get; } = [];
    public ObservableCollection<AccountDocumentUpload> Documents { get; } = [];
    public IReadOnlyList<string> StatusOptions { get; } = ["All statuses", "Active", "Dormant", "Suspended", "Closed", "Frozen"];
    public IReadOnlyList<string> DocumentTypes { get; } = ["Nrc", "Passport", "Visa", "HouseholdRegistration", "ProofOfAddress", "TaxDocument", "SourceOfFunds", "Photo"];
    public IReadOnlyList<string> RenewalInstructions { get; } = ["NoRenewal", "PrincipalOnly", "PrincipalAndInterest"];

    public AsyncRelayCommand SearchAccountsCommand { get; }
    public AsyncRelayCommand LoadMoreCommand { get; }
    public RelayCommand OpenCreateCommand { get; }
    public RelayCommand BackToListCommand { get; }
    public AsyncRelayCommand OpenAccountCommand { get; }
    public AsyncRelayCommand CreateAccountCommand { get; }
    public RelayCommand AddDocumentCommand { get; }
    public RelayCommand RemoveDocumentCommand { get; }

    public string Screen { get => _screen; private set => SetProperty(ref _screen, value); }
    public bool IsListScreen => Screen == "List";
    public bool IsCreateScreen => Screen == "Create";
    public bool IsDetailScreen => Screen == "Detail";
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
    public bool IsLoadingInterestRateRules { get; private set; }
    public bool HasNoInterestRateRules => SelectedCreateType is not null && !IsLoadingInterestRateRules && InterestRateRules.Count == 0;
    public AccountResponse? SelectedAccount { get => _selectedAccount; private set => SetProperty(ref _selectedAccount, value); }
    public string ErrorMessage { get => _errorMessage; private set => SetProperty(ref _errorMessage, value); }
    public string InfoMessage { get => _infoMessage; private set => SetProperty(ref _infoMessage, value); }
    public string OpeningBalance { get => _openingBalance; set => SetProperty(ref _openingBalance, value); }
    public string HolderNrc1 { get => _holderNrc1; set => SetProperty(ref _holderNrc1, value); }
    public string HolderNrc2 { get => _holderNrc2; set => SetProperty(ref _holderNrc2, value); }
    public string OwnershipPercentage1 { get => _ownershipPercentage1; set => SetProperty(ref _ownershipPercentage1, value); }
    public string OwnershipPercentage2 { get => _ownershipPercentage2; set => SetProperty(ref _ownershipPercentage2, value); }
    public string SigningRule { get => _signingRule; set => SetProperty(ref _signingRule, value); }
    public bool IsSharedAccount { get => _isSharedAccount; set => SetProperty(ref _isSharedAccount, value); }
    public string PayoutAccountId { get => _payoutAccountId; set => SetProperty(ref _payoutAccountId, value); }
    public string RenewalInstruction { get => _renewalInstruction; set => SetProperty(ref _renewalInstruction, value); }
    public bool CalculateFromCurrent { get => _calculateFromCurrent; set => SetProperty(ref _calculateFromCurrent, value); }
    public string SelectedDocumentType { get; set; } = "Nrc";
    public string SelectedDocumentNumber { get; set; } = string.Empty;
    public string SelectedDocumentPath { get => _selectedDocumentPath; set => SetProperty(ref _selectedDocumentPath, value); }
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                LoadMoreCommand.RaiseCanExecuteChanged();
                CreateAccountCommand.RaiseCanExecuteChanged();
                OpenAccountCommand.RaiseCanExecuteChanged();
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
        });
    }

    private async Task LoadAccountsAsync(bool append)
    {
        await RunBusyAsync(() => LoadAccountsCoreAsync(append, CancellationToken.None));
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
            new AccountListCriteria(SearchText, SelectedFilterType?.Id, status, append ? _nextCursor : null),
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
        InfoMessage = Accounts.Count == 0 ? "No accounts match these filters." : $"Showing {Accounts.Count} account(s).";
    }

    private void ShowCreate()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        ResetCreateForm();
        Screen = "Create";
        NotifyScreenChanged();
    }

    private void ShowList()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
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
            SelectedAccount = await _accountService.GetAccountAsync(summary.Id, CancellationToken.None);
            Screen = "Detail";
            NotifyScreenChanged();
        });
    }

    private async Task CreateAccountAsync()
    {
        ErrorMessage = string.Empty;
        if (SelectedCreateType is null)
        {
            ErrorMessage = "Choose an account type.";
            return;
        }

        if (string.IsNullOrWhiteSpace(HolderNrc1))
        {
            ErrorMessage = "Enter the primary holder's NRC number.";
            return;
        }

        if (!decimal.TryParse(OpeningBalance, NumberStyles.Number, CultureInfo.InvariantCulture, out var openingBalance))
        {
            ErrorMessage = "Enter a valid opening balance.";
            return;
        }

        if (openingBalance < SelectedCreateType.MinimumOpeningBalance)
        {
            ErrorMessage = $"The minimum opening balance for this product is {SelectedCreateType.MinimumOpeningBalance:N2}.";
            return;
        }

        if (IsSharedAccount && (string.IsNullOrWhiteSpace(HolderNrc2) ||
            !decimal.TryParse(OwnershipPercentage1, NumberStyles.Number, CultureInfo.InvariantCulture, out _) ||
            !decimal.TryParse(OwnershipPercentage2, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
        {
            ErrorMessage = "Enter both holder NRCs and valid ownership percentages for a shared account.";
            return;
        }

        if (IsFixedDeposit && (!TryOptionalLong(PayoutAccountId, out var payoutAccountId) || payoutAccountId is null ||
            SelectedInterestRateRule is null))
        {
            ErrorMessage = "Enter a valid payout account ID and choose an interest rate rule for this fixed deposit.";
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
            Documents.ToArray(),
            IsFixedDeposit && TryOptionalLong(PayoutAccountId, out var payout) ? payout : null,
            IsFixedDeposit ? SelectedInterestRateRule?.Id : null,
            IsFixedDeposit ? RenewalInstruction : null,
            IsFixedDeposit ? CalculateFromCurrent : null);

        await RunBusyAsync(async () =>
        {
            SelectedAccount = await _accountService.CreateAccountAsync(request, CancellationToken.None);
            Screen = "Detail";
            InfoMessage = "Account created successfully.";
            NotifyScreenChanged();
        });
    }

    private void AddSelectedDocument()
    {
        if (string.IsNullOrWhiteSpace(SelectedDocumentPath) || !File.Exists(SelectedDocumentPath))
        {
            ErrorMessage = "Choose an existing document file first.";
            return;
        }

        if (Documents.Any(document => document.DocumentType == SelectedDocumentType))
        {
            ErrorMessage = "Only one document per document type can be uploaded.";
            return;
        }

        Documents.Add(new AccountDocumentUpload(SelectedDocumentType, NullIfBlank(SelectedDocumentNumber), SelectedDocumentPath));
        SelectedDocumentPath = string.Empty;
        SelectedDocumentNumber = string.Empty;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(SelectedDocumentPath));
        OnPropertyChanged(nameof(SelectedDocumentNumber));
    }

    private void RemoveDocument(object? parameter)
    {
        if (parameter is AccountDocumentUpload document)
        {
            Documents.Remove(document);
        }
    }

    private void ResetCreateForm()
    {
        SelectedCreateType = null;
        OpeningBalance = "0";
        HolderNrc1 = string.Empty;
        HolderNrc2 = string.Empty;
        OwnershipPercentage1 = "100";
        OwnershipPercentage2 = "0";
        SigningRule = string.Empty;
        IsSharedAccount = false;
        PayoutAccountId = string.Empty;
        SelectedInterestRateRule = null;
        RenewalInstruction = RenewalInstructions[0];
        CalculateFromCurrent = false;
        Documents.Clear();
        SelectedDocumentPath = string.Empty;
        SelectedDocumentNumber = string.Empty;
        OnPropertyChanged(nameof(SelectedDocumentPath));
        OnPropertyChanged(nameof(SelectedDocumentNumber));
    }

    private async Task RunBusyAsync(Func<Task> action)
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
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (IOException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (Exception)
        {
            ErrorMessage = "Something went wrong while communicating with the account service.";
        }
        finally
        {
            IsBusy = false;
        }
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
                ErrorMessage = exception is AppException or IOException
                    ? exception.Message
                    : "Could not load interest rate rules for this account type.";
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

    private void NotifyScreenChanged()
    {
        OnPropertyChanged(nameof(IsListScreen));
        OnPropertyChanged(nameof(IsCreateScreen));
        OnPropertyChanged(nameof(IsDetailScreen));
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static decimal? TryOptionalDecimal(string value) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : null;
    private static bool TryOptionalLong(string value, out long? result)
    {
        result = long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        return string.IsNullOrWhiteSpace(value) || result.HasValue;
    }
}
