using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Models;

/// <summary>
/// One debit or credit line inside a journal transaction block, with the presentation state the table needs:
/// the Dr/Cr side, whether it belongs to the bank or a customer, and whether it matches the page's focus.
/// </summary>
public sealed class AccountingJournalLineDisplayModel
{
    private const string DebitLabel = "Dr";
    private const string CreditLabel = "Cr";

    // Builds a line; transactionDescription lets the line hide a description identical to its transaction header.
    public AccountingJournalLineDisplayModel(AccountingEntryResponse entry, bool isHighlighted, string? transactionDescription)
    {
        Entry = entry;
        IsHighlighted = isHighlighted;
        ExtraDescription = string.IsNullOrWhiteSpace(entry.Description) ||
                           string.Equals(entry.Description, transactionDescription, StringComparison.Ordinal)
            ? null
            : entry.Description;
    }

    public AccountingEntryResponse Entry { get; }

    public bool IsDebit => Entry.EntryType == EntryType.Debit;

    public string SideLabel => IsDebit ? DebitLabel : CreditLabel;

    public string GlAccountCode => Entry.GlAccountCode;

    public string GlAccountName => Entry.GlAccountName;

    public string? CustomerAccountNumber => Entry.CustomerAccountNumber;

    public string? CustomerAccountOwnerName => Entry.CustomerAccountOwnerName;

    public bool IsBankInternal => Entry.IsBankInternal;

    public decimal? DebitAmount => Entry.DebitAmount;

    public decimal? CreditAmount => Entry.CreditAmount;

    /// <summary>True when the line posts to the GL being viewed, or matches the active filter.</summary>
    public bool IsHighlighted { get; }

    /// <summary>The line's own description, shown only when it differs from the transaction description.</summary>
    public string? ExtraDescription { get; }
}
