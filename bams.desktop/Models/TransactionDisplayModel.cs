using bams.desktop.DTOs.Transactions;
using bams.desktop.Utils;

namespace bams.desktop.Models;

/// <summary>
/// One row of a transaction table, shaped for binding. The flags decide which row actions an officer sees.
/// </summary>
public sealed class TransactionDisplayModel
{
    public long Id { get; init; }

    public string TransactionNo { get; init; } = string.Empty;

    public TransactionType Type { get; init; }

    public TransactionStatus Status { get; init; }

    public string TypeText => TransactionDisplay.ToDisplayName(Type);

    public string StatusText => TransactionDisplay.ToDisplayName(Status);

    public decimal Amount { get; init; }

    /// <summary>"1,204,550.00" (the column header names the currency).</summary>
    public string AmountText => TransactionDisplay.FormatAmount(Amount);

    /// <summary>Local transaction time.</summary>
    public DateTime TransactionAt { get; init; }

    public string TransactionAtText => TransactionAt.ToString(Constants.DisplayFormats.DateTime);

    public string ReferenceText { get; init; } = string.Empty;

    /// <summary>A pending NRC transfer can be picked up or cancelled.</summary>
    public bool IsPendingNrcTransfer => Type == TransactionType.NrcTransfer && Status == TransactionStatus.Pending;

    /// <summary>A pending interbank transfer waits for the gateway result: settled or failed.</summary>
    public bool IsPendingInterbankTransfer => Type == TransactionType.InterbankTransfer && Status == TransactionStatus.Pending;

    /// <summary>
    /// Builds a row from the server list item.
    /// </summary>
    public static TransactionDisplayModel FromResponse(TransactionSummaryResponse transaction)
    {
        return new TransactionDisplayModel
        {
            Id = transaction.Id,
            TransactionNo = transaction.TransactionNo,
            Type = transaction.TransactionType,
            Status = transaction.TransactionStatus,
            Amount = transaction.Amount,
            TransactionAt = DateTimeDisplay.ToLocal(transaction.TransactionAt),
            ReferenceText = TransactionDisplay.OrDash(transaction.ReferenceNo)
        };
    }
}
