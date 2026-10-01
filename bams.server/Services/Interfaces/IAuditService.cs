using bams.server.DTOs.Audit;

namespace bams.server.Services.Interfaces;

/// <summary>
/// Provides read-only access to system audit history.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Retrieves audit logs ordered from newest to oldest.
    /// </summary>
    Task<IReadOnlyList<AuditLogResponse>> GetAuditLogsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves one audit log by its unique identifier.
    /// </summary>
    Task<AuditLogResponse> GetAuditLogByIdAsync(long auditLogId, CancellationToken cancellationToken);
}