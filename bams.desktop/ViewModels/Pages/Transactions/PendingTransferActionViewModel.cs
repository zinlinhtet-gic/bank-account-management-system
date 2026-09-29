using bams.desktop.Commands;
using bams.desktop.Constants;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Exceptions;
using bams.desktop.Models;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// What a <see cref="PendingTransferActionViewModel"/> does to a pending transfer.
/// </summary>
public enum PendingTransferAction
{
    CancelNrcTransfer,
    RecordNrcPayout,
    CompleteInterbankTransfer,
    FailInterbankTransfer
}

/// <summary>
/// Small form that finishes a pending transfer: cancel an NRC transfer, record that another bank paid one out, or
/// record the gateway result of an interbank transfer (settled or failed). Cancel and fail refund the sender.
/// Closes with <c>true</c> when done.
/// </summary>
public sealed class PendingTransferActionViewModel : ViewModelBase, IDialogViewModel
{
    private readonly ITransactionService _transactionService;
    private readonly TransactionDisplayModel _transfer;

    private string _gatewayReference = string.Empty;
    private string _settlementReference = string.Empty;
    private string _reason = string.Empty;
    private string _formError = string.Empty;
    private bool _isBusy;

    public PendingTransferActionViewModel(
        ITransactionService transactionService,
        TransactionDisplayModel transfer,
        PendingTransferAction action)
    {
        _transactionService = transactionService;
        _transfer = transfer;
        Action = action;

        SaveCommand = new AsyncRelayCommand(SubmitAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false), _ => CanCancel);
    }

    public event Action<bool>? CloseRequested;

    public bool CanCloseOnBackdropClick => false;

    public bool CanCancel => !IsBusy;

    public PendingTransferAction Action { get; }

    public AsyncRelayCommand SaveCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>The refund (cancel / fail) or the settled transfer, set just before the dialog closes.</summary>
    public TransactionResponse? Result { get; private set; }

    public string Title => Action switch
    {
        PendingTransferAction.CancelNrcTransfer => "Cancel NRC transfer?",
        PendingTransferAction.RecordNrcPayout => "Record payout by the other bank",
        PendingTransferAction.CompleteInterbankTransfer => "Mark transfer as settled",
        _ => "Mark transfer as failed?"
    };

    public string Subtitle => $"{_transfer.TransactionNo} · {TransactionDisplay.FormatMoney(_transfer.Amount)}";

    public string Explanation => Action switch
    {
        PendingTransferAction.CancelNrcTransfer =>
            "The pickup code stops working and the sender gets the money back the way they paid: cash or their account.",
        PendingTransferAction.RecordNrcPayout =>
            "Record this after the other bank confirms it paid the receiver. The transfer is then completed.",
        PendingTransferAction.CompleteInterbankTransfer =>
            "Record this after the payment gateway confirms the other bank received the money.",
        _ => "Record this when the gateway rejected the transfer. The amount goes back to the sender's account."
    };

    /// <summary>Cancel and fail refund the sender and cannot be undone, so they use the danger style.</summary>
    public bool IsDestructive => Action is PendingTransferAction.CancelNrcTransfer or PendingTransferAction.FailInterbankTransfer;

    public string IconKey => IsDestructive ? "Icon.AlertCircle" : "Icon.Check";

    public string SaveText => Action switch
    {
        PendingTransferAction.CancelNrcTransfer => "Cancel and refund",
        PendingTransferAction.RecordNrcPayout => "Record payout",
        PendingTransferAction.CompleteInterbankTransfer => "Mark as settled",
        _ => "Mark failed and refund"
    };

    public string CancelText => Action == PendingTransferAction.CancelNrcTransfer ? "Keep transfer" : "Cancel";

    public bool ShowsGatewayReference => Action != PendingTransferAction.CancelNrcTransfer;

    public string GatewayReferenceLabel => Action == PendingTransferAction.RecordNrcPayout
        ? "REFERENCE FROM THE OTHER BANK (OPTIONAL)"
        : "GATEWAY REFERENCE (OPTIONAL)";

    public bool ShowsSettlementReference => Action == PendingTransferAction.CompleteInterbankTransfer;

    public bool ShowsReason => IsDestructive;

    public string GatewayReference
    {
        get => _gatewayReference;
        set => SetProperty(ref _gatewayReference, value);
    }

    public string SettlementReference
    {
        get => _settlementReference;
        set => SetProperty(ref _settlementReference, value);
    }

    /// <summary>Stored as the refund's description, e.g. "Receiver did not collect".</summary>
    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

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

    // Checks the optional texts fit, then sends the action for this transfer.
    private async Task SubmitAsync()
    {
        FormError = string.Empty;

        if (GatewayReference.Trim().Length > TransactionFieldRules.GatewayReferenceMaximumLength
            || SettlementReference.Trim().Length > TransactionFieldRules.GatewayReferenceMaximumLength
            || Reason.Trim().Length > TransactionFieldRules.DescriptionMaximumLength)
        {
            FormError = MessageCatalog.GetMessage(MessageCode.FieldTooLong);
            return;
        }

        try
        {
            IsBusy = true;
            Result = Action switch
            {
                PendingTransferAction.CancelNrcTransfer => await _transactionService.CancelNrcTransferAsync(
                    _transfer.Id,
                    new NrcCancelRequest(TrimToNull(Reason)),
                    CancellationToken.None),
                PendingTransferAction.RecordNrcPayout => await _transactionService.RecordNrcPayoutAsync(
                    _transfer.Id,
                    new NrcPayoutRequest(TrimToNull(GatewayReference)),
                    CancellationToken.None),
                PendingTransferAction.CompleteInterbankTransfer => await _transactionService.CompleteInterbankTransferAsync(
                    _transfer.Id,
                    new InterbankSettlementRequest(TrimToNull(GatewayReference), TrimToNull(SettlementReference)),
                    CancellationToken.None),
                _ => await _transactionService.FailInterbankTransferAsync(
                    _transfer.Id,
                    new InterbankFailureRequest(TrimToNull(GatewayReference), TrimToNull(Reason)),
                    CancellationToken.None)
            };
        }
        catch (AppException exception)
        {
            FormError = exception.Message;
            return;
        }
        finally
        {
            IsBusy = false;
        }

        CloseRequested?.Invoke(true);
    }

    private static string? TrimToNull(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
