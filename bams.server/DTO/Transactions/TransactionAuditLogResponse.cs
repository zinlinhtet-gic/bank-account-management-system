namespace bams.server.DTO.Transactions;

/// <summary>
/// One audit event recorded against a transaction.
/// </summary>
public sealed record TransactionAuditLogResponse(
    long Id,
    long UserId,
    string Username,
    string FullName,
    string Action,
    string? Details,
    string? IpAddress,
    string? DeviceInfo,
    DateTime CreatedAt);