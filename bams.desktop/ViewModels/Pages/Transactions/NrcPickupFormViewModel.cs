using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// NRC pickup at one of our branches: the officer enters the name and NRC from the collector's NRC card and the
/// six-digit code, and the server pays out in cash only when they match the receiver the sender designated. Closes
/// itself with <c>true</c> when the pickup completed. Mismatches and wrong codes are counted by the server, which
/// blocks the transfer after too many.
/// </summary>
public sealed class NrcPickupFormViewModel : ViewModelBase, IDialogViewModel
{
    private readonly ITransactionService _transactionService;
    private readonly TransactionDetailResponse _transfer;

    private string _receiverName = string.Empty;
    private string _receiverNrc = string.Empty;
    private string _pickupCode = string.Empty;
    private string _cashSessionIdText = string.Empty;
    private string _formError = string.Empty;
    private bool _isBusy;

    public NrcPickupFormViewModel(
        ITransactionService transactionService,
        TransactionDetailResponse transfer)
    {
        _transactionService = transactionService;
        _transfer = transfer;

        SaveCommand = new AsyncRelayCommand(CompletePickupAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false), _ => CanCancel);
    }

    public event Action<bool>? CloseRequested;

    public bool CanCloseOnBackdropClick => false;

    public bool CanCancel => !IsBusy;

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    public string Subtitle => $"{_transfer.TransactionNo} · {TransactionDisplay.FormatMoney(_transfer.Amount)}";

    public string SenderText => _transfer.NrcTransfer is { } nrc ? $"{nrc.SenderName} · {nrc.SenderNrc}" : DisplayFormats.EmptyValue;

    public string ExpiresText => TransactionDisplay.FormatTimestamp(_transfer.NrcTransfer?.PickupExpiresAt);

    public string PickupLocationText => TransactionDisplay.OrDash(_transfer.NrcTransfer?.PickupLocation);

    /// <summary>Earlier failed pickups, shown as a warning so the officer knows the transfer is close to being blocked.</summary>
    public string AttemptsWarning => _transfer.NrcTransfer is { FailedPickupAttempts: > 0 } nrc
        ? $"{nrc.FailedPickupAttempts} failed pickup{(nrc.FailedPickupAttempts == 1 ? " was" : "s were")} recorded before. Too many failed pickups block the transfer."
        : string.Empty;

    public bool HasAttemptsWarning => !string.IsNullOrEmpty(AttemptsWarning);

    public int PersonNameMaximumLength => TransactionFieldRules.PersonNameMaximumLength;

    public int NrcMaximumLength => TransactionFieldRules.NrcMaximumLength;

    public int PickupCodeLength => TransactionFieldRules.PickupCodeLength;

    /// <summary>Full name as printed on the collector's NRC card.</summary>
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

    public FieldError ReceiverNameError { get; } = new();

    /// <summary>NRC number as printed on the collector's NRC card.</summary>
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

    public FieldError ReceiverNrcError { get; } = new();

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
    public FieldError CashSessionError { get; } = new();

    public string CashSessionIdText
    {
        get => _cashSessionIdText;
        set { if (SetProperty(ref _cashSessionIdText, value)) CashSessionError.Clear(); }
    }

    /// <summary>Error that belongs to no single field (identity mismatch, expired, blocked, network...).</summary>
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

    // Checks the collector's identity fields and the code, then asks the server to verify them and pay out in cash.
    private async Task CompletePickupAsync()
    {
        FormError = string.Empty;

        var name = ReceiverName.Trim();
        var nrc = ReceiverNrc.Trim();
        var code = PickupCode.Trim();
        ReceiverNameError.Set(name.Length == 0 ? "Enter the name on the collector's NRC card." : string.Empty);
        ReceiverNrcError.Set(nrc.Length == 0 ? "Enter the NRC number on the collector's NRC card." : string.Empty);
        PickupCodeError.Set(code.Length != TransactionFieldRules.PickupCodeLength || !code.All(char.IsAsciiDigit)
            ? $"Enter the {TransactionFieldRules.PickupCodeLength}-digit code the sender received."
            : string.Empty);
        CashSessionError.Set(!long.TryParse(CashSessionIdText, out var cashSessionId) || cashSessionId <= 0
            ? "Enter an open teller session ID for this branch."
            : string.Empty);

        if (ReceiverNameError.HasError || ReceiverNrcError.HasError || PickupCodeError.HasError || CashSessionError.HasError)
        {
            return;
        }

        try
        {
            IsBusy = true;
            await _transactionService.CompleteNrcPickupAsync(
                new NrcPickupRequest(_transfer.Id, code, name, nrc, long.Parse(CashSessionIdText)),
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
