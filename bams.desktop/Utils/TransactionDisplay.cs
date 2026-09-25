using System.Globalization;
using bams.desktop.Constants;
using bams.desktop.DTOs.Transactions;

namespace bams.desktop.Utils;

/// <summary>
/// Display text for transaction values. Every screen that shows a transaction type, status or amount uses these,
/// so the wording and number format are the same everywhere.
/// </summary>
public static class TransactionDisplay
{
    /// <summary>e.g. "Cash deposit", "NRC transfer", "Refund".</summary>
    public static string ToDisplayName(TransactionType type) => type switch
    {
        TransactionType.CashDeposit => "Cash deposit",
        TransactionType.CashWithdrawal => "Cash withdrawal",
        TransactionType.InternalTransfer => "Internal transfer",
        TransactionType.InterbankTransfer => "Interbank transfer",
        TransactionType.NrcTransfer => "NRC transfer",
        TransactionType.NrcPickup => "NRC pickup",
        TransactionType.InterestCredit => "Interest credit",
        TransactionType.MaintenanceFee => "Maintenance fee",
        TransactionType.Penalty => "Penalty",
        TransactionType.FdMaturity => "FD maturity",
        TransactionType.FdEarlyWithdrawal => "FD early withdrawal",
        TransactionType.Reversal => "Refund",
        _ => type.ToString()
    };

    /// <summary>e.g. "Completed", "Pending".</summary>
    public static string ToDisplayName(TransactionStatus status) => status.ToString();

    /// <summary>"1,204,550.00": thousands separators and two decimals, without the currency.</summary>
    public static string FormatAmount(decimal amount)
    {
        return amount.ToString(DisplayFormats.Amount, CultureInfo.CurrentCulture);
    }

    /// <summary>"MMK 1,204,550.00".</summary>
    public static string FormatMoney(decimal amount)
    {
        return $"{DisplayFormats.CurrencyCode} {FormatAmount(amount)}";
    }

    /// <summary>Local date and time of a server timestamp, or the empty-value dash.</summary>
    public static string FormatTimestamp(DateTime? serverTimestamp)
    {
        return serverTimestamp is null
            ? DisplayFormats.EmptyValue
            : DateTimeDisplay.ToLocal(serverTimestamp.Value).ToString(DisplayFormats.DateTime);
    }

    /// <summary>The text, or the empty-value dash when it is blank.</summary>
    public static string OrDash(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? DisplayFormats.EmptyValue : text;
    }
}
