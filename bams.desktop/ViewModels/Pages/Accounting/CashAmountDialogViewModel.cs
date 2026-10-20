using bams.desktop.Commands;

namespace bams.desktop.ViewModels.Pages.Accounting;

/// <summary>Collects a manager-selected amount only when a cash request omitted it.</summary>
public sealed class CashAmountDialogViewModel : ViewModelBase, IDialogViewModel
{
    private string _text = "", _error = "";
    public string AmountText { get => _text; set => SetProperty(ref _text, value); }
    public bams.desktop.Utils.MessageCode? ErrorCode => string.IsNullOrEmpty(Error) ? null : bams.desktop.Utils.MessageCode.ValidationFailed;
    public string Error { get => _error; private set { SetProperty(ref _error, value); OnPropertyChanged(nameof(ErrorCode)); } }
    public decimal Amount { get; private set; }
    public event Action<bool>? CloseRequested;
    public bool CanCloseOnBackdropClick => false;
    public bool CanCancel => true;
    public RelayCommand ConfirmCommand { get; }
    public RelayCommand CancelCommand { get; }
    public CashAmountDialogViewModel()
    {
        ConfirmCommand = new(_ =>
        {
            if (!decimal.TryParse(AmountText, out var amount) || amount <= 0 || decimal.Round(amount, 2) != amount)
            { Error = "Enter a positive amount with at most two decimal places."; return; }
            Amount = amount; CloseRequested?.Invoke(true);
        });
        CancelCommand = new(_ => CloseRequested?.Invoke(false));
    }
}
