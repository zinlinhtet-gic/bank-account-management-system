namespace bams.server.DTO.Accounts;

/// <summary>
/// Describes an amount to add to or subtract from an account balance.
/// </summary>
public sealed record UpdateAccountBalanceRequest(
    decimal Amount,
    long Version);
