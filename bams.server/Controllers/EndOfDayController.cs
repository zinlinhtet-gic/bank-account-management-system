using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/operations/business-date")]
public sealed class EndOfDayController(IEndOfDayWorkflowService workflow) : ControllerBase
{
    [HttpGet]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.Audit, SecurityConstants.CashOperations)]
    public async Task<ActionResult<ApiMessageResponse<BusinessDateResponse>>> GetCurrentBusinessDateAsync(CancellationToken cancellationToken)
    {
        var result = await workflow.GetCurrentBusinessDateAsync(cancellationToken);
        return Ok(ApiMessageResponse<BusinessDateResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("{date}/pre-close")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.Audit)]
    public async Task<ActionResult<ApiMessageResponse<EndOfDayRunResponse>>> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var result = await workflow.RunPreCloseAsync(date, cancellationToken);
        return Ok(ApiMessageResponse<EndOfDayRunResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("runs/{id:long}/approve")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<EndOfDayRunResponse>>> ApproveAsync(long id, CancellationToken cancellationToken)
    {
        var result = await workflow.ApproveAsync(id, cancellationToken);
        return Ok(ApiMessageResponse<EndOfDayRunResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("runs/{id:long}/close")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<EndOfDayRunResponse>>> CloseAsync(long id, CancellationToken cancellationToken)
    {
        var result = await workflow.CloseAsync(id, cancellationToken);
        return Ok(ApiMessageResponse<EndOfDayRunResponse>.FromCode(MessageCode.Success, result));
    }
}
