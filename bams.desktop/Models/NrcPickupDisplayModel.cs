using bams.desktop.DTOs.Transactions;
using bams.desktop.Utils;

namespace bams.desktop.Models;

/// <summary>
/// One row of the NRC pickup table, shaped for binding. Expired and blocked transfers stay listed so the officer can
/// tell the collector why the money cannot be paid out, but cannot be picked up.
/// </summary>
public sealed class NrcPickupDisplayModel
{
    public long TransactionId { get; init; }

    public string TransactionNo { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    /// <summary>"1,204,550.00" (the column header names the currency).</summary>
    public string AmountText => TransactionDisplay.FormatAmount(Amount);

    public string SenderName { get; init; } = string.Empty;

    public string ReceiverName { get; init; } = string.Empty;

    public string PickupBranchText { get; init; } = string.Empty;

    public string SentAtText { get; init; } = string.Empty;

    public string ExpiresAtText { get; init; } = string.Empty;

    public int FailedPickupAttempts { get; init; }

    public bool IsExpired { get; init; }

    public bool IsBlocked { get; init; }

    /// <summary>The pickup form can be opened: the code is still valid and not blocked by failed attempts.</summary>
    public bool CanPickUp => !IsExpired && !IsBlocked;

    /// <summary>Blocked wins over expired: a new code is needed either way, but blocked explains more.</summary>
    public string StateText => IsBlocked ? "Blocked" : IsExpired ? "Expired" : "Ready";

    public string StateToolTip => CanPickUp
        ? FailedPickupAttempts > 0
            ? $"{FailedPickupAttempts} failed pickup attempt{(FailedPickupAttempts == 1 ? string.Empty : "s")} so far."
            : "Ready to collect with the receiver's NRC card and the pickup code."
        : "Cannot be collected. Issue a new code or cancel it on the Transaction History page.";

    /// <summary>
    /// Builds a row from the server list item.
    /// </summary>
    public static NrcPickupDisplayModel FromResponse(NrcPickupSummaryResponse pickup)
    {
        return new NrcPickupDisplayModel
        {
            TransactionId = pickup.TransactionId,
            TransactionNo = pickup.TransactionNo,
            Amount = pickup.Amount,
            SenderName = pickup.SenderName,
            ReceiverName = pickup.ReceiverName,
            PickupBranchText = TransactionDisplay.OrDash(pickup.PickupBranchName),
            SentAtText = TransactionDisplay.FormatTimestamp(pickup.TransactionAt),
            ExpiresAtText = TransactionDisplay.FormatTimestamp(pickup.PickupExpiresAt),
            FailedPickupAttempts = pickup.FailedPickupAttempts,
            IsExpired = pickup.IsExpired,
            IsBlocked = pickup.IsBlocked
        };
    }
}
