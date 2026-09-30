using System.Text.Json.Serialization;
namespace bams.desktop.DTOs.Accounting;

public sealed record AccountingEntryResponse(
    long Id,
    long TransactionId,
    long GlAccountId,
    string GlAccountCode,
    string GlAccountName,
    long? CustomerAccountId,
    EntryType EntryType,
    decimal Amount,
    DateOnly PostingDate,
    string? Description,
    DateTime CreatedAt
);
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EntryType
{
    Debit = 1,
    Credit = 2
}