using bams.server.Models.Accounts;

namespace bams.server.DTO.Accounts;

public sealed record AccountSummaryResponse(
    long Id,
    string AccountNo,
    string AccountTypeCode,
    AccountStatus Status,
    decimal AvailableBalance);
