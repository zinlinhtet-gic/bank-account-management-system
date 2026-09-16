using bams.server.Models.Security;

namespace bams.server.Models.Audit;

public sealed class AuditLog
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public User? User { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string EntityId { get; set; } = string.Empty;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public string? DeviceInfo { get; set; }

    public DateTime CreatedAt { get; set; }
}
