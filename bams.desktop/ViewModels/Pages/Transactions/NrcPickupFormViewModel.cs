using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// NRC pickup at one of our branches: shows who may collect the money so the officer can check their NRC, takes the
/// six-digit code and pays out in cash or into the receiver's account. Closes itself with <c>true</c> when the pickup
/// completed. Wrong codes are counted by the server, which blocks the transfer after too many.
/// </summary>
public sealed class NrcPickupFormViewModel : ViewModelBase, IDialogViewModel
{
    private readonly ITransactionService _transactionService;
    private readonly TransactionDetailResponse _transfer;

    private string _pickupCode = string.Empty;
    private bool _isPaidInCash = true;
    private AccountOption? _destinationAccount;
    private string _formError = string.Empty;
    private bool _isBusy;

    public NrcPickupFormViewModel(
        ITransactionService transactionService,
        TransactionDetailResponse transfer,
        IReadOnlyList<AccountOption> accounts)
    {
        _transactionService = transactionService;
        _transfer = transfer;
        Accounts = accounts;

        SaveCommand = new AsyncRelayCommand(CompletePickupAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false), _ => CanCancel);
    }

    public event Action<bool>? CloseRequested;

    public bool CanCloseOnBackdropClick => false;

    public bool CanCancel => !IsBusy;

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public string Subtitle => $"{_transfer.TransactionNo} · {TransactionDisplay.FormatMoney(_transfer.Amount)}";

    public string ReceiverName => _transfer.NrcTransfer?.ReceiverName ?? DisplayFormats.EmptyValue;

    public string ReceiverNrc => _transfer.NrcTransfer?.ReceiverNrc ?? DisplayFormats.EmptyValue;

    public string SenderText => _transfer.NrcTransfer is { } nrc ? $"{nrc.SenderName} · {nrc.SenderNrc}" : DisplayFormats.EmptyValue;

    public string ExpiresText => TransactionDisplay.FormatTimestamp(_transfer.NrcTransfer?.PickupExpiresAt);

    public string PickupLocationText => TransactionDisplay.OrDash(_transfer.NrcTransfer?.PickupLocation);

    /// <summary>The receiver's possible accounts with us, for a payout into an account.</summary>
    public IReadOnlyList<AccountOption> Accounts { get; }

    /// <summary>Pay the receiver in cash over the counter (no account needed).</summary>
    public bool IsPaidInCash
    {
        get => _isPaidInCash;
        set
        {
            if (SetProperty(ref _isPaidInCash, value))
            {
                DestinationAccountError.Clear();
                OnPropertyChanged(nameof(IsPaidIntoAccount));
            }
        }
    }

    /// <summary>Pay the money into the receiver's account with us.</summary>
    public bool IsPaidIntoAccount
    {
        get => !IsPaidInCash;
        set => IsPaidInCash = !value;
    }

    public AccountOption? DestinationAccount
    {
        get => _destinationAccount;
        set
        {
            if (SetProperty(ref _destinationAccount, value))
            {
                DestinationAccountError.Clear();
            }
        }
    }

    public FieldError DestinationAccountError { get; } = new();

    /// <summary>Earlier wrong codes, shown as a warning so the officer knows the transfer is close to being blocked.</summary>
    public string AttemptsWarning => _transfer.NrcTransfer is { FailedPickupAttempts: > 0 } nrc
        ? $"{nrc.FailedPickupAttempts} wrong code{(nrc.FailedPickupAttempts == 1 ? " was" : "s were")} entered before. Too many wrong codes block the transfer."
        : string.Empty;

    public bool HasAttemptsWarning => !string.IsNullOrEmpty(AttemptsWarning);

    public int PickupCodeLength => TransactionFieldRules.PickupCodeLength;

    public string PickupCode
    {
        get => _pickupCode;
        set
        {
            if (SetProperty(ref _pickupCode, value))
            {
                PickupCodeError.Clear();
            }
        }
    }

    public FieldError PickupCodeError { get; } = new();

    /// <summary>Error that belongs to no single field (expired, blocked, network...).</summary>
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
                OnPropertyChanged(nameof(CanCancel));
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    // Checks the code and payout choice, then asks the server to verify the code and pay out.
    private async Task CompletePickupAsync()
    {
        FormError = string.Empty;

        var code = PickupCode.Trim();
        PickupCodeError.Set(code.Length != TransactionFieldRules.PickupCodeLength || !code.All(char.IsAsciiDigit)
            ? $"Enter the {TransactionFieldRules.PickupCodeLength}-digit code the sender received."
            : string.Empty);
        DestinationAccountError.Set(IsPaidIntoAccount && DestinationAccount is null
            ? "Please choose the receiver's account."
            : string.Empty);

        if (PickupCodeError.HasError || DestinationAccountError.HasError)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _transactionService.CompleteNrcPickupAsync(
                new NrcPickupRequest(_transfer.Id, code, IsPaidIntoAccount ? DestinationAccount!.Id : null),
                CancellationToken.None);
        }
        catch (AppException exception)
        {
            if (exception.Code == MessageCode.InvalidPickupCode)
            {
                PickupCodeError.Set(exception.Message);
            }
            else
            {
                FormError = exception.Message;
            }

            return;
        }
        finally
        {
            IsBusy = false;
        }

        CloseRequested?.Invoke(true);
    }
}
