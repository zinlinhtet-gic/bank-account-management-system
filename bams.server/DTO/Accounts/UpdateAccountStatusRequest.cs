namespace bams.server.DTO.Accounts;

/// <summary>
/// Describes the audit reason and expected version for a dedicated account-status action.
/// </summary>
public sealed record UpdateAccountStatusRequest(
    string? Reason,
    long Version);
