using bams.server.DTO.Audit;

namespace bams.server.Services.Interfaces;
/// <summary>
/// Performs end-of-day accounting validation and reconciliation.
/// </summary>
public interface IEndOfDayAuditService
{
    /// <summary>
    /// Audits accounting activity for the specified accounting date.
    /// </summary>
    Task<EndOfDayAuditResult> RunEndOfDayAuditAsync(DateOnly auditDate, CancellationToken cancellationToken);
}