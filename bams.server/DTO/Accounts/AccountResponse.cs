using bams.server.Models.Accounts;

namespace bams.server.DTO.Accounts;

public sealed record AccountResponse(
    long Id,
    string AccountNo,
    long AccountTypeId,
    string AccountTypeCode,
    AccountStatus Status,
    decimal AvailableBalance,
    decimal LedgerBalance,
    DateTime OpenedAt,
    DateTime CreatedAt);
