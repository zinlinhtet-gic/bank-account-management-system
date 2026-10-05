using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/cash-operations")]
public sealed class CashOperationsController(ICashOperationsService cashOperations) : ControllerBase
{
    [HttpGet("sessions")]
    [RequirePermission(SecurityConstants.CashOperations, SecurityConstants.Audit, SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<CashPositionSessionResponse>>>> GetSessionsAsync(
        [FromQuery] DateOnly? businessDate, CancellationToken cancellationToken)
    {
        var sessions = await cashOperations.GetSessionsAsync(businessDate, cancellationToken);
        return Ok(ApiMessageResponse<IReadOnlyList<CashPositionSessionResponse>>.FromCode(MessageCode.Success, sessions));
    }

    [HttpGet("sessions/{id:long}")]
    [RequirePermission(SecurityConstants.CashOperations, SecurityConstants.Audit, SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<CashPositionSessionDetailResponse>>> GetSessionDetailAsync(
        long id, CancellationToken cancellationToken)
    {
        var detail = await cashOperations.GetSessionDetailAsync(id, cancellationToken);
        return Ok(ApiMessageResponse<CashPositionSessionDetailResponse>.FromCode(MessageCode.Success, detail));
    }

    [HttpPost("sessions")]
    [RequirePermission(SecurityConstants.CashOperations)]
    [AllowWithoutOpenCashSession]
    public async Task<ActionResult<ApiMessageResponse<CashPositionSessionResponse>>> OpenSessionAsync(
        OpenCashSessionRequest request, [FromHeader(Name = TransactionConstants.IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var session = await cashOperations.OpenSessionAsync(request, idempotencyKey, cancellationToken);
        return Ok(ApiMessageResponse<CashPositionSessionResponse>.FromCode(MessageCode.Success, session));
    }

    [HttpPost("sessions/{id:long}/transfers")]
    [RequirePermission(SecurityConstants.CashOperations)]
    public async Task<ActionResult<ApiMessageResponse<CashPositionSessionResponse>>> TransferCashAsync(
        long id, TransferCashRequest request, CancellationToken cancellationToken)
    {
        var session = await cashOperations.TransferCashAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashPositionSessionResponse>.FromCode(MessageCode.Success, session));
    }

    [HttpPost("sessions/{id:long}/count")]
    [RequirePermission(SecurityConstants.CashOperations)]
    public async Task<ActionResult<ApiMessageResponse<CashCountResponse>>> SubmitCountAsync(
        long id, SubmitCashCountRequest request, [FromHeader(Name = TransactionConstants.IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var count = await cashOperations.SubmitCountAsync(id, request, idempotencyKey, cancellationToken);
        return Ok(ApiMessageResponse<CashCountResponse>.FromCode(MessageCode.Success, count));
    }

    [HttpGet("adjustments")]
    [RequirePermission(SecurityConstants.CashOperations, SecurityConstants.EndOfDayApproval, SecurityConstants.Audit)]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<CashAdjustmentResponse>>>> GetAdjustmentsAsync(
        [FromQuery] DateOnly? businessDate, [FromQuery] string? status, CancellationToken cancellationToken)
    {
        var adjustments = await cashOperations.GetAdjustmentsAsync(businessDate, status, cancellationToken);
        return Ok(ApiMessageResponse<IReadOnlyList<CashAdjustmentResponse>>.FromCode(MessageCode.Success, adjustments));
    }

    [HttpPost("sessions/{id:long}/adjustments")]
    [RequirePermission(SecurityConstants.CashOperations)]
    public async Task<ActionResult<ApiMessageResponse<CashAdjustmentResponse>>> RequestAdjustmentAsync(
        long id, RequestCashAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var adjustment = await cashOperations.RequestAdjustmentAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashAdjustmentResponse>.FromCode(MessageCode.Success, adjustment));
    }

    [HttpPost("adjustments/{id:long}/approve")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<CashAdjustmentResponse>>> ApproveAdjustmentAsync(long id, CancellationToken cancellationToken)
    {
        var adjustment = await cashOperations.ApproveAdjustmentAsync(id, cancellationToken);
        return Ok(ApiMessageResponse<CashAdjustmentResponse>.FromCode(MessageCode.Success, adjustment));
    }
}
