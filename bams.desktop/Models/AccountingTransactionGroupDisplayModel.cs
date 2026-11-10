using bams.desktop.DTOs.Accounting;
using bams.desktop.Utils;

namespace bams.desktop.Models;

/// <summary>
/// Groups all general-ledger journal lines for one transaction and exposes its shared display metadata:
/// the header (number, type, date, description), Dr/Cr totals and whether the journal balances.
/// </summary>
public sealed class AccountingTransactionGroupDisplayModel
{
    private const string DateFormat = "dd/MM/yyyy";
    private const string TimeFormat = "HH:mm";

    // Builds one transaction block. isHighlighted marks lines that match the page's focus (GL or filter).
    public AccountingTransactionGroupDisplayModel(
        IReadOnlyList<AccountingEntryResponse> entries,
        Func<AccountingEntryResponse, bool>? isHighlighted = null)
    {
        Entries = entries;
        var firstEntry = entries[0];
        TransactionId = firstEntry.TransactionId;
        TransactionNo = firstEntry.TransactionNo;
        TransactionAt = firstEntry.TransactionAt;
        TransactionTypeName = TransactionDisplay.ToDisplayName(firstEntry.TransactionType);
        Description = entries.Select(entry => entry.Description).FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
        TotalDebit = entries.Sum(entry => entry.DebitAmount ?? 0m);
        TotalCredit = entries.Sum(entry => entry.CreditAmount ?? 0m);
        PostingDateText = BuildPostingDateText(firstEntry.PostingDate, TransactionAt);

        // Debit lines first, matching accounting convention, then in posting order.
        Lines = entries
            .OrderBy(entry => entry.EntryType == EntryType.Debit ? 0 : 1)
            .ThenBy(entry => entry.Id)
            .Select(entry => new AccountingJournalLineDisplayModel(entry, isHighlighted?.Invoke(entry) ?? false, Description))
            .ToList();
    }

    public long TransactionId { get; }

    public string TransactionNo { get; }

    public DateTime TransactionAt { get; }

    public string TransactionTypeName { get; }

    public string? Description { get; }

    public string TransactionDate => MyanmarTimeDisplay.ToMyanmarTime(TransactionAt).ToString(DateFormat);

    public string TransactionTime => MyanmarTimeDisplay.ToMyanmarTime(TransactionAt).ToString(TimeFormat);

    /// <summary>"· Posted dd/MM/yyyy" when the ledger posting date differs from the transaction date; otherwise null.</summary>
    public string? PostingDateText { get; }

    public decimal TotalDebit { get; }

    public decimal TotalCredit { get; }

    public string TotalsText => $"Dr {MoneyText.Format(TotalDebit)} = Cr {MoneyText.Format(TotalCredit)}";

    public bool IsBalanced => TotalDebit == TotalCredit;

    public IReadOnlyList<AccountingEntryResponse> Entries { get; }

    public IReadOnlyList<AccountingJournalLineDisplayModel> Lines { get; }

    /// <summary>
    /// Groups a page of journal lines into transaction blocks, newest transaction first.
    /// Shared by the Accounting Entries and GL Account Detail pages.
    /// </summary>
    public static IEnumerable<AccountingTransactionGroupDisplayModel> BuildGroups(
        IEnumerable<AccountingEntryResponse> entries,
        Func<AccountingEntryResponse, bool>? isHighlighted)
    {
        return entries
            .GroupBy(entry => entry.TransactionId)
            .OrderByDescending(group => group.First().TransactionAt)
            .ThenByDescending(group => group.Key)
            .Select(group => new AccountingTransactionGroupDisplayModel(group.ToList(), isHighlighted));
    }

    // The posting (business) date only needs calling out when it is not the calendar date of the transaction.
    private static string? BuildPostingDateText(DateOnly postingDate, DateTime transactionAt)
    {
        var transactionDate = DateOnly.FromDateTime(MyanmarTimeDisplay.ToMyanmarTime(transactionAt));
        return postingDate == transactionDate ? null : $"· Posted {postingDate.ToString(DateFormat)}";
    }
}
