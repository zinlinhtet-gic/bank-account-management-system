using bams.desktop.Commands;
using bams.desktop.DTOs.Operations;
using bams.desktop.Exceptions;
using bams.desktop.Services;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Operations;

/// <summary>
/// The "Break deposit early" panel of the fixed deposit card: shows the server's quote (interest earned, forfeited
/// and the net payout), then breaks the deposit. Anyone who sees the card can view the quote; only users with the
/// early-withdrawal permission (managers) can confirm it. The server enforces both.
/// </summary>
public sealed class FixedDepositEarlyWithdrawalViewModel : ViewModelBase
{
    private readonly IOperationService _operationService;
    private readonly long _fixedDepositId;

    // One key per card: a retry after a network error resends it, so the server pays at most once.
    private readonly string _idempotencyKey = Guid.NewGuid().ToString("N");

    private FixedDepositEarlyWithdrawalQuoteResponse? _quote;
    private string _reason = string.Empty;
    private string _errorMessage = string.Empty;
    private string _resultMessage = string.Empty;
    private bool _isBusy;

    /// <param name="isEligible">The deposit is active and before its maturity date.</param>
    /// <param name="canWithdraw">The signed-in user holds the early-withdrawal permission.</param>
    public FixedDepositEarlyWithdrawalViewModel(
        IOperationService operationService,
        long fixedDepositId,
        bool isEligible,
        bool canWithdraw)
    {
        _operationService = operationService;
        _fixedDepositId = fixedDepositId;
        IsEligible = isEligible;
        CanWithdraw = canWithdraw;

        ShowQuoteCommand = new AsyncRelayCommand(LoadQuoteAsync, () => !IsBusy && !IsCompleted);
        HideQuoteCommand = new RelayCommand(_ => HideQuote(), _ => !IsBusy);
        WithdrawCommand = new AsyncRelayCommand(WithdrawAsync, () => CanWithdraw && HasQuote && !IsBusy && !IsCompleted);
    }

    /// <summary>Raised once the deposit has been broken, so the card can tell the list to reload.</summary>
    public event Action? Completed;

    public AsyncRelayCommand ShowQuoteCommand { get; }

    public RelayCommand HideQuoteCommand { get; }

    public AsyncRelayCommand WithdrawCommand { get; }

    /// <summary>Only an active deposit before maturity can be broken; at maturity the maturity run pays it out.</summary>
    public bool IsEligible { get; }

    public bool CanWithdraw { get; }

    /// <summary>Shows the "Break deposit early" button.</summary>
    public bool ShowsStartButton => IsEligible && !HasQuote && !IsCompleted;

    public bool HasQuote => _quote is not null;

    /// <summary>Shows the breakdown and confirm panel.</summary>
    public bool ShowsQuote => HasQuote && !IsCompleted;

    public bool IsCompleted => !string.IsNullOrEmpty(ResultMessage);

    public string Principal => FormatQuoteMoney(quote => quote.Principal);
    public string DaysHeld => _quote is null ? string.Empty : $"{_quote.DaysHeld} days";
    public string ContractRate => _quote is null ? string.Empty : OperationDisplay.FormatRate(_quote.AnnualRate);
    public string EarlyWithdrawalRate => _quote is null ? string.Empty : OperationDisplay.FormatRate(_quote.EarlyWithdrawalRate);
    public string InterestEarned => FormatQuoteMoney(quote => quote.InterestEarned);
    public string InterestKept => FormatQuoteMoney(quote => quote.InterestAtEarlyWithdrawalRate);
    public string InterestForfeited => FormatQuoteMoney(quote => quote.InterestForfeited);
    public string NetPayout => FormatQuoteMoney(quote => quote.NetPayout);
    public string PayoutAccountNo => _quote?.PayoutAccountNo ?? string.Empty;

    /// <summary>Optional note on why the customer breaks the deposit; kept in the audit log.</summary>
    public string Reason
    {
        get => _reason;
        set => SetProperty(ref _reason, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

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

    /// <summary>e.g. "Paid 1,012,345.00 MMK to 0002… · TXN… · interest forfeited 4,321.00 MMK".</summary>
    public string ResultMessage
    {
        get => _resultMessage;
        private set
        {
            if (SetProperty(ref _resultMessage, value))
            {
                OnPropertyChanged(nameof(IsCompleted));
                OnPropertyChanged(nameof(ShowsStartButton));
                OnPropertyChanged(nameof(ShowsQuote));
                RaiseCommandStates();
            }
        }
    }

    // Asks the server what breaking the deposit today would pay. Nothing is posted.
    private async Task LoadQuoteAsync()
    {
        ErrorMessage = string.Empty;

        try
        {
            IsBusy = true;
            ApplyQuote(await _operationService.GetEarlyWithdrawalQuoteAsync(_fixedDepositId, CancellationToken.None));
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Breaks the deposit. The server recalculates the amounts; the result shows what was actually paid.
    private async Task WithdrawAsync()
    {
        ErrorMessage = string.Empty;

        try
        {
            IsBusy = true;
            var request = new EarlyWithdrawFixedDepositRequest(string.IsNullOrWhiteSpace(Reason) ? null : Reason.Trim());
            var transaction = await _operationService.WithdrawFixedDepositEarlyAsync(
                _fixedDepositId, request, _idempotencyKey, CancellationToken.None);

            ResultMessage = BuildResultMessage(transaction);
            Completed?.Invoke();
        }
        catch (AppException exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void HideQuote()
    {
        ErrorMessage = string.Empty;
        ApplyQuote(null);
    }

    // Swaps the shown quote and refreshes every value bound to it.
    private void ApplyQuote(FixedDepositEarlyWithdrawalQuoteResponse? quote)
    {
        _quote = quote;
        OnPropertyChanged(string.Empty);
        RaiseCommandStates();
    }

    private string BuildResultMessage(DTOs.Transactions.TransactionResponse transaction)
    {
        var message = $"{MessageCatalog.GetMessage(MessageCode.FixedDepositWithdrawnEarlySuccessfully)} " +
            $"Paid {OperationDisplay.FormatMoney(transaction.Amount)} to {PayoutAccountNo} · {transaction.TransactionNo}.";

        // The forfeited interest is stored as the transaction's fee.
        return transaction.FeeAmount > 0m
            ? $"{message} Interest forfeited: {OperationDisplay.FormatMoney(transaction.FeeAmount)}."
            : message;
    }

    private string FormatQuoteMoney(Func<FixedDepositEarlyWithdrawalQuoteResponse, decimal> select)
    {
        return _quote is null ? string.Empty : OperationDisplay.FormatMoney(select(_quote));
    }

    private void RaiseCommandStates()
    {
        ShowQuoteCommand.RaiseCanExecuteChanged();
        HideQuoteCommand.RaiseCanExecuteChanged();
        WithdrawCommand.RaiseCanExecuteChanged();
    }
}
