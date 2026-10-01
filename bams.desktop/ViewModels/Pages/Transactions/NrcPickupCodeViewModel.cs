using bams.desktop.Commands;
using bams.desktop.DTOs.Transactions;
using bams.desktop.Utils;

namespace bams.desktop.ViewModels.Pages.Transactions;

/// <summary>
/// Shown once, right after an NRC transfer is created: the pickup code the sender passes to the receiver.
/// The server keeps only a hash, so this is the only time the code can be read.
/// </summary>
public sealed class NrcPickupCodeViewModel : ViewModelBase, IDialogViewModel
{
    public NrcPickupCodeViewModel(TransactionResponse transfer, string receiverName, string pickupLocation)
    {
        PickupCode = transfer.PickupCode ?? string.Empty;
        Subtitle = $"{transfer.TransactionNo} · {TransactionDisplay.FormatMoney(transfer.Amount)} for {receiverName}";
        PickupLocation = pickupLocation;

        CloseCommand = new RelayCommand(_ => CloseRequested?.Invoke(true));
    }

    public event Action<bool>? CloseRequested;

    // The code cannot be shown again, so a stray click must not close it.
    public bool CanCloseOnBackdropClick => false;

    public bool CanCancel => true;

    public string PickupCode { get; }

    public string Subtitle { get; }

    /// <summary>Where the receiver collects, e.g. "Mandalay Branch".</summary>
    public string PickupLocation { get; }

    public RelayCommand CloseCommand { get; }
}
