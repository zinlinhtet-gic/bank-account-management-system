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
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.Audit, SecurityConstants.CashOperations, SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<BusinessDateResponse>>> GetCurrentBusinessDateAsync(CancellationToken cancellationToken)
    {
        var result = await workflow.GetCurrentBusinessDateAsync(cancellationToken);
        return Ok(ApiMessageResponse<BusinessDateResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpGet("dates")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.Audit, SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<BusinessDateResponse>>>> SearchBusinessDatesAsync(
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken cancellationToken)
    {
        var result = await workflow.SearchBusinessDatesAsync(fromDate, toDate, cancellationToken);
        return Ok(ApiMessageResponse<IReadOnlyList<BusinessDateResponse>>.FromCode(MessageCode.Success, result));
    }

    [HttpGet("dates/{date}/latest-run")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.Audit, SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<EndOfDayRunResponse?>>> GetLatestRunAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var result = await workflow.GetLatestRunAsync(date, cancellationToken);
        return Ok(ApiMessageResponse<EndOfDayRunResponse?>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("{date}/pre-close")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.Audit)]
    public async Task<ActionResult<ApiMessageResponse<EndOfDayRunResponse>>> RunPreCloseAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var result = await workflow.RunPreCloseAsync(date, cancellationToken);
        return Ok(ApiMessageResponse<EndOfDayRunResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("{date}/review-pre-close")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<EndOfDayRunResponse>>> ReviewPreCloseAsync(DateOnly date, CancellationToken cancellationToken)
    {
        var result = await workflow.ReviewPreCloseAsync(date, cancellationToken);
        return Ok(ApiMessageResponse<EndOfDayRunResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("{date}/force-close-request")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<EndOfDayRunResponse>>> RequestForceCloseAsync(DateOnly date,
        [FromBody] ForceCloseRequest request, CancellationToken cancellationToken)
    {
        var result = await workflow.RequestForceCloseAsync(date, request.Reason, cancellationToken);
        return Ok(ApiMessageResponse<EndOfDayRunResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("runs/{id:long}/force-close-approve")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<EndOfDayRunResponse>>> ApproveForceCloseAsync(long id, CancellationToken cancellationToken)
    {
        var result = await workflow.ApproveForceCloseAsync(id, cancellationToken);
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
