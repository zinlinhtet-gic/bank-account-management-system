using bams.server.Models.Transactions;

namespace bams.server.DTO.Accounting;
public sealed record AccountingEntryResponse
(
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
    // Primary holder's full name; null for bank-level GL lines that have no customer account.
    string? CustomerAccountOwnerName,
    EntryType EntryType,
    decimal Amount,
    DateOnly PostingDate,
    string? Description,
    DateTime CreatedAt
);
