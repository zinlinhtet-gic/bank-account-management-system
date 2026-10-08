using System.Text.Json.Serialization;
using bams.desktop.DTOs.Transactions;
namespace bams.desktop.DTOs.Accounting;

public sealed record AccountingEntryResponse(
    long Id,
    long TransactionId,
    string TransactionNo,
    TransactionType TransactionType,
    DateTime TransactionAt,
    long GlAccountId,
    string GlAccountCode,
    string GlAccountName,
    long? CustomerAccountId,
    string? CustomerAccountNumber,
    EntryType EntryType,
    decimal Amount,
    DateOnly PostingDate,
    string? Description,
    DateTime CreatedAt
)
{
    public decimal? DebitAmount => EntryType == EntryType.Debit ? Amount : null;

    public decimal? CreditAmount => EntryType == EntryType.Credit ? Amount : null;
}
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntryType
{
    Debit = 1,
    Credit = 2
}
