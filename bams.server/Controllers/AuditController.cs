using bams.server.Middlewares;
using bams.server.Constants;
using bams.server.DTO.Audit;
using bams.server.DTOs.Audit;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/audit")]
// Audit logs and the end-of-day close are restricted to users holding the audit permission.
[RequirePermission(SecurityConstants.Audit)]
public sealed class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly IEndOfDayAuditService _endOfDayAuditService;

    public AuditController(
        IAuditService auditService,
        IEndOfDayAuditService endOfDayAuditService)
    {
        _auditService = auditService;
        _endOfDayAuditService = endOfDayAuditService;
    }

    // Returns system audit logs ordered from newest to oldest.
    [HttpGet("logs")]
    public async Task<ActionResult<IReadOnlyList<AuditLogResponse>>>
        GetAuditLogsAsync(
            CancellationToken cancellationToken)
    {
        var auditLogs =
            await _auditService.GetAuditLogsAsync(
                cancellationToken);

        return Ok(auditLogs);
    }

    // Returns one audit log by its unique identifier.
    [HttpGet("logs/{id:long}")]
    public async Task<ActionResult<AuditLogResponse>>
        GetAuditLogByIdAsync(
            long id,
            CancellationToken cancellationToken)
    {
        var auditLog =
            await _auditService.GetAuditLogByIdAsync(
                id,
                cancellationToken);

        return Ok(auditLog);
    }

    // Runs the end-of-day accounting audit for the requested date.
    [Authorize]
    [HttpPost("end-of-day")]
    public async Task<ActionResult<EndOfDayAuditResult>>RunEndOfDayAuditAsync(
            [FromQuery] DateOnly date,
            CancellationToken cancellationToken)
    {
        var result = await _endOfDayAuditService.RunEndOfDayAuditAsync(date,cancellationToken);
        return Ok(result);
    }
}