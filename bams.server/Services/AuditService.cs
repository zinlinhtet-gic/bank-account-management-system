using bams.server.Data;
using bams.server.DTOs.Audit;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AuditService : IAuditService
{
    private readonly ApplicationDbContext _dbContext;

    public AuditService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Retrieves audit logs ordered from newest to oldest.
    /// </summary>
    public async Task<IReadOnlyList<AuditLogResponse>> GetAuditLogsAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.AuditLogs
            .AsNoTracking()
            .OrderByDescending(log => log.CreatedAt)
            .Select(log => new AuditLogResponse(
                log.Id,
                log.UserId,
                log.User != null
                    ? log.User.Username
                    : string.Empty,
                log.User != null
                    ? log.User.FullName
                    : string.Empty,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.OldValues,
                log.NewValues,
                log.IpAddress,
                log.DeviceInfo,
                log.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves one audit log by its unique identifier.
    /// </summary>
    public async Task<AuditLogResponse> GetAuditLogByIdAsync(
        long auditLogId,
        CancellationToken cancellationToken)
    {
        var auditLog = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(log => log.Id == auditLogId)
            .Select(log => new AuditLogResponse(
                log.Id,
                log.UserId,
                log.User != null
                    ? log.User.Username
                    : string.Empty,
                log.User != null
                    ? log.User.FullName
                    : string.Empty,
                log.Action,
                log.EntityType,
                log.EntityId,
                log.OldValues,
                log.NewValues,
                log.IpAddress,
                log.DeviceInfo,
                log.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);

        if (auditLog is null)
        {
            throw new NotFoundException(MessageCode.ResourceNotFound);
        }

        return auditLog;
    }
}