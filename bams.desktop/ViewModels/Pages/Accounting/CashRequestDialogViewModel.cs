using bams.desktop.Commands;
using bams.desktop.DTOs.Accounting;
using bams.desktop.Exceptions;
using bams.desktop.Services;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Collects an officer's cash amount and reason before submitting a vault request.</summary>
public sealed class CashRequestDialogViewModel : ViewModelBase, IDialogViewModel
{
    private readonly ICashOperationsClientService _service;
    private readonly long _sessionId;
    private readonly string _sessionTitle;
    private string _amountText = string.Empty;
    private string _reason = string.Empty;
    private string _formError = string.Empty;
    private string _amountError = string.Empty;
    private string _reasonError = string.Empty;
    private bool _isBusy;

    public CashRequestDialogViewModel(ICashOperationsClientService service, long sessionId, string sessionTitle)
    {
        _service = service;
        _sessionId = sessionId;
        _sessionTitle = sessionTitle;
        SubmitCommand = new AsyncRelayCommand(SubmitAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => CloseRequested?.Invoke(false), _ => CanCancel);
    }

    public event Action<bool>? CloseRequested;
    public bool CanCloseOnBackdropClick => false;
    public bool CanCancel => !IsBusy;
    public string SessionTitle => _sessionTitle;
    public AsyncRelayCommand SubmitCommand { get; }
    public RelayCommand CancelCommand { get; }
    public string AmountText { get => _amountText; set { if (SetProperty(ref _amountText, value)) { AmountError = string.Empty; FormError = string.Empty; } } }
    public string Reason { get => _reason; set { if (SetProperty(ref _reason, value)) { ReasonError = string.Empty; FormError = string.Empty; } } }
    public string AmountError { get => _amountError; private set { if (SetProperty(ref _amountError, value)) OnPropertyChanged(nameof(HasAmountError)); } }
    public bool HasAmountError => !string.IsNullOrEmpty(AmountError);
    public string ReasonError { get => _reasonError; private set { if (SetProperty(ref _reasonError, value)) OnPropertyChanged(nameof(HasReasonError)); } }
    public bool HasReasonError => !string.IsNullOrEmpty(ReasonError);
    public string FormError { get => _formError; private set { if (SetProperty(ref _formError, value)) OnPropertyChanged(nameof(HasFormError)); } }
    public bool HasFormError => !string.IsNullOrWhiteSpace(FormError);
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            SubmitCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task SubmitAsync()
    {
        AmountError = string.Empty;
        ReasonError = string.Empty;
        FormError = string.Empty;

        decimal? amount = null;
        if (!string.IsNullOrWhiteSpace(AmountText))
        {
            if (!decimal.TryParse(AmountText, out var parsed) || parsed <= 0m || decimal.Round(parsed, 2) != parsed)
            {
                AmountError = "Enter an amount greater than 0 with no more than 2 decimal places, or leave it blank.";
                return;
            }
            amount = parsed;
        }

        var reason = Reason.Trim();
        if (reason.Length == 0)
        {
            ReasonError = "Enter a reason for the cash request.";
            return;
        }
        if (reason.Length > 500)
        {
            ReasonError = "The reason must be 500 characters or fewer.";
            return;
        }

        try
        {
            IsBusy = true;
            await _service.CreateCashProposalAsync(_sessionId, new CreateCashProposalRequest(amount, reason), CancellationToken.None);
            CloseRequested?.Invoke(true);
        }
        catch (AppException exception) { FormError = exception.Message; }
        finally { IsBusy = false; }
    }
}
