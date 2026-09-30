using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Models;

/// <summary>
/// Groups all general-ledger journal lines for one transaction and exposes its shared display metadata.
/// </summary>
public sealed class AccountingTransactionGroupDisplayModel
{
    public AccountingTransactionGroupDisplayModel(IReadOnlyList<AccountingEntryResponse> entries)
    {
        Entries = entries;
        var firstEntry = entries[0];
        TransactionId = firstEntry.TransactionId;
        TransactionNo = firstEntry.TransactionNo;
        TransactionAt = firstEntry.TransactionAt;
    }

    public long TransactionId { get; }

    public string TransactionNo { get; }

    public DateTime TransactionAt { get; }

    public string TransactionDate => TransactionAt.ToString("yyyy-MM-dd");

    public string TransactionTime => TransactionAt.ToString("HH:mm");

    public IReadOnlyList<AccountingEntryResponse> Entries { get; }
}
