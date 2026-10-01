using System.Globalization;
using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// Which posting a <see cref="TransactionFormViewModel"/> creates. It decides the fields shown and the endpoint called.
/// </summary>
public enum TransactionFormKind
{
    Deposit,
    Withdrawal,
    InternalTransfer,
    InterbankTransfer,
    NrcTransfer
}

/// <summary>
/// The form for a new deposit, withdrawal or transfer, shown in the Transactions page's tab. One form serves every
/// kind; only the fields that kind needs are visible. Raises <see cref="Posted"/> after a successful posting
/// (<see cref="SavedTransaction"/> holds the result, including the NRC pickup code) and <see cref="ClearRequested"/>
/// when the officer clears it; the page then puts a fresh form in its place.
/// </summary>
/// <remarks>
/// Each form instance has its own idempotency key. Retrying after a network error resends the same key, so the
/// server returns the first result instead of posting the money twice.
/// </remarks>
public sealed class TransactionFormViewModel : ViewModelBase
{
    private readonly ITransactionService _transactionService;
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");

    private AccountOption? _sourceAccount;
    private AccountOption? _destinationAccount;
    private OtherBankResponse? _selectedBank;
    private string _amountText = string.Empty;
    private string _description = string.Empty;
    private string _referenceNo = string.Empty;
    private string _destinationAccountNo = string.Empty;
    private string _beneficiaryName = string.Empty;
    private string _senderName = string.Empty;
    private string _senderNrc = string.Empty;
    private string _senderPhone = string.Empty;
    private string _receiverName = string.Empty;
    private string _receiverNrc = string.Empty;
    private string _receiverPhone = string.Empty;
    private bool _isPaidInCash = true;
    private bool _isCollectedAtBranch = true;
    private BranchResponse? _pickupBranch;
    private OtherBankResponse? _pickupBank;
    private string _formError = string.Empty;
    private bool _isBusy;

    /// <summary>
    /// Creates the form. <paramref name="banks"/> is used by the interbank form and as NRC pickup locations;
    /// <paramref name="branches"/> only by the NRC form.
    /// </summary>
    public TransactionFormViewModel(
        ITransactionService transactionService,
        TransactionFormKind kind,
        IReadOnlyList<AccountOption> accounts,
        IReadOnlyList<OtherBankResponse> banks,
        IReadOnlyList<BranchResponse> branches)
    {
        _transactionService = transactionService;
        Kind = kind;
        Accounts = accounts;
        Banks = banks;
        Branches = branches;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        ClearCommand = new RelayCommand(_ => ClearRequested?.Invoke(), _ => !IsBusy);
    }

    /// <summary>Raised after the transaction was posted; <see cref="SavedTransaction"/> is set.</summary>
    public event Action? Posted;

    /// <summary>Raised when the officer clears the form to start over.</summary>
    public event Action? ClearRequested;

    public TransactionFormKind Kind { get; }

    public IReadOnlyList<AccountOption> Accounts { get; }

    public IReadOnlyList<OtherBankResponse> Banks { get; }

    public IReadOnlyList<BranchResponse> Branches { get; }

    /// <summary>NRC: another bank can be the pickup location only when some are set up.</summary>
    public bool HasOtherBanks => Banks.Count > 0;

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand ClearCommand { get; }

    /// <summary>The posted transaction, set just before <see cref="Posted"/> is raised.</summary>
    public TransactionResponse? SavedTransaction { get; private set; }

    // ===== Texts that depend on the kind =====

    public string Title => Kind switch
    {
        TransactionFormKind.Deposit => "Cash deposit",
        TransactionFormKind.Withdrawal => "Cash withdrawal",
        TransactionFormKind.InternalTransfer => "Internal transfer",
        TransactionFormKind.InterbankTransfer => "Interbank transfer",
        _ => "NRC transfer"
    };

    public string Subtitle => Kind switch
    {
        TransactionFormKind.Deposit => "Record cash paid into a customer account.",
        TransactionFormKind.Withdrawal => "Pay out cash from a customer account.",
        TransactionFormKind.InternalTransfer => "Move money between two accounts in this bank.",
        TransactionFormKind.InterbankTransfer => "Send money to another bank. It stays pending until settled.",
        _ => "The receiver collects the money at a branch or another bank with their NRC and a pickup code."
    };

    public string SaveText => Kind switch
    {
        TransactionFormKind.Deposit => "Deposit",
        TransactionFormKind.Withdrawal => "Withdraw",
        TransactionFormKind.InternalTransfer => "Transfer",
        TransactionFormKind.InterbankTransfer => "Submit transfer",
        _ => "Create transfer"
    };

    /// <summary>Header icon resource key.</summary>
    public string IconKey => Kind switch
    {
        TransactionFormKind.Deposit => "Icon.Download",
        TransactionFormKind.Withdrawal => "Icon.Upload",
        TransactionFormKind.InternalTransfer => "Icon.Transfers",
        TransactionFormKind.InterbankTransfer => "Icon.Bank",
        _ => "Icon.Send"
    };

    /// <summary>
    /// Money comes out of a source account for every kind except a deposit, and except an NRC transfer the sender
    /// pays in cash.
    /// </summary>
    public bool ShowsSourceAccount => Kind != TransactionFormKind.Deposit
        && !(Kind == TransactionFormKind.NrcTransfer && IsPaidInCash);

    /// <summary>The account picker at the top of the form; the NRC form shows it inside the sender section instead.</summary>
    public bool ShowsSourceAccountAtTop => ShowsSourceAccount && Kind != TransactionFormKind.NrcTransfer;

    /// <summary>Deposits and internal transfers credit an account in this bank.</summary>
    public bool ShowsDestinationAccount => Kind is TransactionFormKind.Deposit or TransactionFormKind.InternalTransfer;

    public bool ShowsBankFields => Kind == TransactionFormKind.InterbankTransfer;

    public bool ShowsNrcFields => Kind == TransactionFormKind.NrcTransfer;

    public string SourceAccountLabel => Kind switch
    {
        TransactionFormKind.Withdrawal => "ACCOUNT",
        TransactionFormKind.NrcTransfer => "SENDER'S ACCOUNT",
        _ => "FROM ACCOUNT"
    };

    public string DestinationAccountLabel => Kind == TransactionFormKind.Deposit ? "ACCOUNT" : "TO ACCOUNT";

    // ===== NRC: how the sender pays and where the receiver collects =====

    /// <summary>NRC: the sender hands over cash at the counter (no account needed).</summary>
    public bool IsPaidInCash
    {
        get => _isPaidInCash;
        set
        {
            if (SetProperty(ref _isPaidInCash, value))
            {
                SourceAccountError.Clear();
                OnPropertyChanged(nameof(IsPaidFromAccount));
                OnPropertyChanged(nameof(ShowsSourceAccount));
            }
        }
    }

    /// <summary>NRC: the sender pays from their account with us.</summary>
    public bool IsPaidFromAccount
    {
        get => !IsPaidInCash;
        set => IsPaidInCash = !value;
    }

    /// <summary>NRC: the receiver collects at one of our branches.</summary>
    public bool IsCollectedAtBranch
    {
        get => _isCollectedAtBranch;
        set
        {
            if (SetProperty(ref _isCollectedAtBranch, value))
            {
                PickupLocationError.Clear();
                OnPropertyChanged(nameof(IsCollectedAtOtherBank));
            }
        }
    }

    /// <summary>NRC: the receiver collects at another bank.</summary>
    public bool IsCollectedAtOtherBank
    {
        get => !IsCollectedAtBranch;
        set => IsCollectedAtBranch = !value;
    }

    public BranchResponse? PickupBranch
    {
        get => _pickupBranch;
        set
        {
            if (SetProperty(ref _pickupBranch, value))
            {
                PickupLocationError.Clear();
            }
        }
    }

    public OtherBankResponse? PickupBank
    {
        get => _pickupBank;
        set
        {
            if (SetProperty(ref _pickupBank, value))
            {
                PickupLocationError.Clear();
            }
        }
    }

    /// <summary>e.g. "Mandalay Branch" or "KBZ Bank", for the pickup code dialog.</summary>
    public string PickupLocationText => (IsCollectedAtBranch ? PickupBranch?.Name : PickupBank?.BankName) ?? string.Empty;

    // ===== Fields =====

    public AccountOption? SourceAccount
    {
        get => _sourceAccount;
        set
        {
            if (SetProperty(ref _sourceAccount, value))
            {
                SourceAccountError.Clear();
                OnPropertyChanged(nameof(SourceBalanceText));
            }
        }
    }

    public string SourceBalanceText => SourceAccount?.BalanceText ?? string.Empty;

    public AccountOption? DestinationAccount
    {
        get => _destinationAccount;
        set
        {
            if (SetProperty(ref _destinationAccount, value))
            {
                DestinationAccountError.Clear();
                OnPropertyChanged(nameof(DestinationBalanceText));
            }
        }
    }

    public string DestinationBalanceText => DestinationAccount?.BalanceText ?? string.Empty;

    public OtherBankResponse? SelectedBank
    {
        get => _selectedBank;
        set
        {
            if (SetProperty(ref _selectedBank, value))
            {
                BankError.Clear();
            }
        }
    }

    /// <summary>The amount as typed, e.g. "10,000" or "250.50".</summary>
    public string AmountText
    {
        get => _amountText;
        set
        {
            if (SetProperty(ref _amountText, value))
            {
                AmountError.Clear();
            }
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string ReferenceNo
    {
        get => _referenceNo;
        set => SetProperty(ref _referenceNo, value);
    }

    public string DestinationAccountNo
    {
        get => _destinationAccountNo;
        set
        {
            if (SetProperty(ref _destinationAccountNo, value))
            {
                DestinationAccountNoError.Clear();
            }
        }
    }

    public string BeneficiaryName
    {
        get => _beneficiaryName;
        set
        {
            if (SetProperty(ref _beneficiaryName, value))
            {
                BeneficiaryNameError.Clear();
            }
        }
    }

    public string SenderName
    {
        get => _senderName;
        set
        {
            if (SetProperty(ref _senderName, value))
            {
                SenderNameError.Clear();
            }
        }
    }

    public string SenderNrc
    {
        get => _senderNrc;
        set
        {
            if (SetProperty(ref _senderNrc, value))
            {
                SenderNrcError.Clear();
            }
        }
    }

    public string SenderPhone
    {
        get => _senderPhone;
        set => SetProperty(ref _senderPhone, value);
    }

    public string ReceiverName
    {
        get => _receiverName;
        set
        {
            if (SetProperty(ref _receiverName, value))
            {
                ReceiverNameError.Clear();
            }
        }
    }

    public string ReceiverNrc
    {
        get => _receiverNrc;
        set
        {
            if (SetProperty(ref _receiverNrc, value))
            {
                ReceiverNrcError.Clear();
            }
        }
    }

    public string ReceiverPhone
    {
        get => _receiverPhone;
        set => SetProperty(ref _receiverPhone, value);
    }

    // ===== Errors =====

    public FieldError SourceAccountError { get; } = new();

    public FieldError DestinationAccountError { get; } = new();

    public FieldError BankError { get; } = new();

    public FieldError AmountError { get; } = new();

    public FieldError DestinationAccountNoError { get; } = new();

    public FieldError BeneficiaryNameError { get; } = new();

    public FieldError SenderNameError { get; } = new();

    public FieldError SenderNrcError { get; } = new();

    public FieldError ReceiverNameError { get; } = new();

    public FieldError ReceiverNrcError { get; } = new();

    public FieldError PickupLocationError { get; } = new();

    /// <summary>Error that belongs to no single field (network, closed account, text too long...).</summary>
    public string FormError
    {
        get => _formError;
        private set
        {
            if (SetProperty(ref _formError, value))
            {
                OnPropertyChanged(nameof(HasFormError));
            }
        }
    }

    public bool HasFormError => !string.IsNullOrEmpty(FormError);

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                ClearCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    /// <summary>
    /// Parses an amount as typed: current-culture or invariant digits, group separators allowed.
    /// Returns null when the text is not a positive amount with at most two decimals.
    /// </summary>
    public static decimal? ParseAmount(string text)
    {
        var trimmed = text.Trim();
        var isNumber = decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

        return isNumber
            && amount > 0m
            && decimal.Round(amount, TransactionFieldRules.MaximumAmountDecimalPlaces) == amount
                ? amount
                : null;
    }

    // Validates locally, then posts the transaction of this form's kind.
    private async Task SaveAsync()
    {
        FormError = string.Empty;

        var amount = ValidateFields();
        if (amount is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            SavedTransaction = await PostAsync(amount.Value);
        }
        catch (AppException exception)
        {
            ShowServerError(exception);
            return;
        }
        finally
        {
            IsBusy = false;
        }

        Posted?.Invoke();
    }

    // Sends the request for this kind with the form's idempotency key.
    private Task<TransactionResponse> PostAsync(decimal amount)
    {
        var description = TrimToNull(Description);
        var referenceNo = TrimToNull(ReferenceNo);

        return Kind switch
        {
            TransactionFormKind.Deposit => _transactionService.DepositAsync(
                new DepositRequest(DestinationAccount!.Id, amount, description, referenceNo),
                _idempotencyKey,
                CancellationToken.None),
            TransactionFormKind.Withdrawal => _transactionService.WithdrawAsync(
                new WithdrawalRequest(SourceAccount!.Id, amount, description, referenceNo),
                _idempotencyKey,
                CancellationToken.None),
            TransactionFormKind.InternalTransfer => _transactionService.TransferInternallyAsync(
                new InternalTransferRequest(SourceAccount!.Id, DestinationAccount!.Id, amount, description, referenceNo),
                _idempotencyKey,
                CancellationToken.None),
            TransactionFormKind.InterbankTransfer => _transactionService.TransferInterbankAsync(
                new InterbankTransferRequest(
                    SourceAccount!.Id,
                    SelectedBank!.Id,
                    DestinationAccountNo.Trim(),
                    BeneficiaryName.Trim(),
                    amount,
                    description,
                    referenceNo),
                _idempotencyKey,
                CancellationToken.None),
            _ => _transactionService.CreateNrcTransferAsync(
                new NrcTransferRequest(
                    IsPaidInCash ? null : SourceAccount!.Id,
                    SenderName.Trim(),
                    SenderNrc.Trim(),
                    TrimToNull(SenderPhone),
                    ReceiverName.Trim(),
                    ReceiverNrc.Trim(),
                    TrimToNull(ReceiverPhone),
                    IsCollectedAtBranch ? TransactionFieldRules.NrcDeliveryAtBranch : TransactionFieldRules.NrcDeliveryAtOtherBank,
                    IsCollectedAtBranch ? PickupBranch!.Id : null,
                    IsCollectedAtBranch ? null : PickupBank!.Id,
                    amount,
                    description,
                    referenceNo),
                _idempotencyKey,
                CancellationToken.None)
        };
    }

    // Shows every field error at once so the user can fix them in one go; returns the amount when all are valid.
    private decimal? ValidateFields()
    {
        var amount = ParseAmount(AmountText);
        AmountError.Set(AmountText.Trim().Length == 0
            ? "Please enter the amount."
            : amount is null ? MessageCatalog.GetMessage(MessageCode.InvalidAmount) : string.Empty);

        SourceAccountError.Set(ShowsSourceAccount && SourceAccount is null ? "Please choose the account." : string.Empty);

        DestinationAccountError.Set(!ShowsDestinationAccount
            ? string.Empty
            : DestinationAccount is null
                ? "Please choose the account."
                : ShowsSourceAccount && SourceAccount?.Id == DestinationAccount.Id
                    ? MessageCatalog.GetMessage(MessageCode.SameSourceAndDestinationAccount)
                    : string.Empty);

        if (ShowsBankFields)
        {
            BankError.Set(SelectedBank is null ? "Please choose the bank." : string.Empty);
            DestinationAccountNoError.Set(RequiredText(DestinationAccountNo, TransactionFieldRules.DestinationAccountNoMaximumLength,
                "Please enter the account number at the other bank."));
            BeneficiaryNameError.Set(RequiredText(BeneficiaryName, TransactionFieldRules.PersonNameMaximumLength,
                "Please enter the beneficiary's name."));
        }

        if (ShowsNrcFields)
        {
            SenderNameError.Set(RequiredText(SenderName, TransactionFieldRules.PersonNameMaximumLength, "Please enter the sender's name."));
            SenderNrcError.Set(RequiredText(SenderNrc, TransactionFieldRules.NrcMaximumLength, "Please enter the sender's NRC."));
            ReceiverNameError.Set(RequiredText(ReceiverName, TransactionFieldRules.PersonNameMaximumLength, "Please enter the receiver's name."));
            ReceiverNrcError.Set(RequiredText(ReceiverNrc, TransactionFieldRules.NrcMaximumLength, "Please enter the receiver's NRC."));
            PickupLocationError.Set(IsCollectedAtBranch
                ? PickupBranch is null ? "Please choose the branch where the receiver collects." : string.Empty
                : PickupBank is null ? "Please choose the bank where the receiver collects." : string.Empty);

            if (SenderPhone.Trim().Length > TransactionFieldRules.PhoneMaximumLength
                || ReceiverPhone.Trim().Length > TransactionFieldRules.PhoneMaximumLength)
            {
                FormError = MessageCatalog.GetMessage(MessageCode.FieldTooLong);
            }
        }

        if (Description.Trim().Length > TransactionFieldRules.DescriptionMaximumLength
            || ReferenceNo.Trim().Length > TransactionFieldRules.ReferenceNoMaximumLength)
        {
            FormError = MessageCatalog.GetMessage(MessageCode.FieldTooLong);
        }

        var hasFieldError = new[]
        {
            AmountError, SourceAccountError, DestinationAccountError, BankError, DestinationAccountNoError,
            BeneficiaryNameError, SenderNameError, SenderNrcError, ReceiverNameError, ReceiverNrcError, PickupLocationError
        }.Any(error => error.HasError);

        return hasFieldError || HasFormError ? null : amount;
    }

    // Puts a server rejection next to the field it is about; anything else goes to the banner.
    private void ShowServerError(AppException exception)
    {
        switch (exception.Code)
        {
            case MessageCode.InvalidAmount:
            case MessageCode.InsufficientBalance:
            case MessageCode.MinimumBalanceRequired:
            case MessageCode.DailyTransactionLimitExceeded:
            case MessageCode.MonthlyTransactionLimitExceeded:
                AmountError.Set(exception.Message);
                break;
            case MessageCode.WithdrawalNotAllowed:
            case MessageCode.TransferNotAllowed:
                SourceAccountError.Set(exception.Message);
                break;
            case MessageCode.SameSourceAndDestinationAccount:
                DestinationAccountError.Set(exception.Message);
                break;
            case MessageCode.OtherBankNotFound when Kind == TransactionFormKind.NrcTransfer:
            case MessageCode.BranchNotFound:
                PickupLocationError.Set(exception.Message);
                break;
            case MessageCode.OtherBankNotFound:
                BankError.Set(exception.Message);
                break;
            default:
                FormError = exception.Message;
                break;
        }
    }

    // Returns the "required" message for blank text, the "too long" message for long text, or empty when valid.
    private static string RequiredText(string value, int maximumLength, string requiredMessage)
    {
        var trimmed = value.Trim();

        return trimmed.Length == 0
            ? requiredMessage
            : trimmed.Length > maximumLength ? MessageCatalog.GetMessage(MessageCode.FieldTooLong) : string.Empty;
    }

    private static string? TrimToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
