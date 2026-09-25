namespace bams.desktop.DTOs.Accounts;

public sealed record AccountTransactionDetailResponse(
    long Id,
    string TransactionNo,
    string TransactionType,
    string TransactionStatus,
    string EntryType,
    decimal Amount,
    decimal LedgerBalanceAfter,
    decimal AvailableBalanceAfter,
    DateOnly ValueDate,
    DateOnly PostingDate,
    string? Description,
    string? ReferenceNo,
    DateTime CreatedAt);
