namespace bams.server.DTO.Accounts;

/// <summary>
/// Breaks a fixed deposit before maturity.
/// </summary>
/// <param name="Reason">Optional note on why the customer is breaking the deposit; stored in the audit log.</param>
public sealed record EarlyWithdrawFixedDepositRequest(string? Reason);
