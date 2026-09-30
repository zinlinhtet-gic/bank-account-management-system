using bams.server.Models.Transactions;

namespace bams.server.DTO.Accounting;
public sealed record AccountingEntryResponse
(
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