namespace bams.desktop.DTOs.Accounts;

using bams.desktop.DTOs.Transactions;

public sealed record AccountTransactionDetailResponse(
    long Id,
    string TransactionNo,
    TransactionType TransactionType,
    TransactionStatus TransactionStatus,
    EntryType EntryType,
    decimal Amount,
    decimal LedgerBalanceAfter,
    decimal AvailableBalanceAfter,
    DateOnly ValueDate,
    DateOnly PostingDate,
    string? Description,
    string? ReferenceNo,
    DateTime CreatedAt);
