namespace bams.server.DTOs.Audit;

public sealed record AuditLogResponse(
    long Id,
    long UserId,
    string Username,
    string FullName,
    string Action,
    string EntityType,
    string EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    string? DeviceInfo,
    DateTime CreatedAt);