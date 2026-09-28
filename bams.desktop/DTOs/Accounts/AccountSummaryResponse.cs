namespace bams.desktop.DTOs.Accounts;

public sealed record AccountSummaryResponse(
    long Id,
    string AccountNo,
    string AccountTypeCode,
    string Status,
    decimal AvailableBalance);
