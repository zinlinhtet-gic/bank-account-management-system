namespace bams.desktop.DTOs.Accounts;

public sealed record AccountResponse(
    long Id,
    string AccountNo,
    long AccountTypeId,
    string AccountTypeCode,
    string Status,
    decimal AvailableBalance,
    decimal LedgerBalance,
    DateTime OpenedAt,
    DateTime CreatedAt,
    long Version);
