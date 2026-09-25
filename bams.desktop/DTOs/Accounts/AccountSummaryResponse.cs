namespace bams.desktop.DTOs.Accounts;

/// <summary>
/// Mirrors the server's <c>AccountStatus</c>. Member names must match: enums travel as names in JSON.
/// </summary>
public enum AccountStatus
{
    Active = 1,
    Dormant = 2,
    Suspended = 3,
    Closed = 4,
    Frozen = 5
}

/// <summary>
/// One row of <c>GET api/accounts</c> (server: <c>DTO/Accounts/AccountSummaryResponse</c>).
/// </summary>
public sealed record AccountSummaryResponse(
    long Id,
    string AccountNo,
    string AccountTypeCode,
    AccountStatus Status,
    decimal AvailableBalance);
