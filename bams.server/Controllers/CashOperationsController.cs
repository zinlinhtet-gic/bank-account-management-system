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
    /// <summary>Creates a manager's additional schedules as a single batch.</summary>
    [HttpPost("sessions/schedules")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<CashPositionSessionResponse>>>> CreateSchedulesAsync(
        CreateSessionSchedulesRequest request, CancellationToken cancellationToken)
    {
        var result = await cashOperations.CreateSchedulesAsync(request, cancellationToken);
        return Ok(ApiMessageResponse<IReadOnlyList<CashPositionSessionResponse>>.FromCode(MessageCode.Success, result));
    }

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

    [HttpPost("sessions/{id:long}/activate")]
    [RequirePermission(SecurityConstants.CashOperations)]
    [AllowWithoutOpenCashSession]
    public async Task<ActionResult<ApiMessageResponse<CashPositionSessionResponse>>> ActivateSessionAsync(
        long id, ActivateCashSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await cashOperations.ActivateSessionAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashPositionSessionResponse>.FromCode(MessageCode.Success, session));
    }

    [HttpPost("sessions/{id:long}/cancel-scheduled")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<CashPositionSessionResponse>>> CancelScheduledSessionAsync(
        long id, CancellationToken cancellationToken)
    {
        var session = await cashOperations.CancelScheduledSessionAsync(id, cancellationToken);
        return Ok(ApiMessageResponse<CashPositionSessionResponse>.FromCode(MessageCode.Success, session));
    }

    [HttpGet("session-assignees")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<CashSessionAssigneeResponse>>>> GetSessionAssigneesAsync(CancellationToken cancellationToken)
    {
        var result = await cashOperations.GetSessionAssigneesAsync(cancellationToken);
        return Ok(ApiMessageResponse<IReadOnlyList<CashSessionAssigneeResponse>>.FromCode(MessageCode.Success, result));
    }

    [HttpGet("proposals")]
    [RequirePermission(SecurityConstants.CashOperations, SecurityConstants.EndOfDayApproval, SecurityConstants.Audit)]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<CashProposalResponse>>>> GetCashProposalsAsync(
        [FromQuery] DateOnly? businessDate, CancellationToken cancellationToken)
    {
        var result = await cashOperations.GetCashProposalsAsync(businessDate, cancellationToken);
        return Ok(ApiMessageResponse<IReadOnlyList<CashProposalResponse>>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("sessions/{id:long}/proposals")]
    [RequirePermission(SecurityConstants.CashOperations)]
    public async Task<ActionResult<ApiMessageResponse<CashProposalResponse>>> CreateCashProposalAsync(
        long id, CreateCashProposalRequest request, CancellationToken cancellationToken)
    {
        var result = await cashOperations.CreateCashProposalAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashProposalResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("proposals/{id:long}/approve")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<CashProposalResponse>>> ApproveCashProposalAsync(
        long id, ApproveCashProposalRequest request, CancellationToken cancellationToken)
    {
        var result = await cashOperations.ApproveCashProposalAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashProposalResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("proposals/{id:long}/receive")]
    [AllowWithoutOpenCashSession]
    [RequirePermission(SecurityConstants.CashOperations)]
    public async Task<ActionResult<ApiMessageResponse<CashProposalResponse>>> ReceiveCashProposalAsync(
        long id, CashProposalActionRequest request, CancellationToken cancellationToken)
    {
        var result = await cashOperations.ReceiveCashProposalAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashProposalResponse>.FromCode(MessageCode.Success, result));
    }

    [HttpPost("proposals/{id:long}/reject")]
    [AllowWithoutOpenCashSession]
    [RequirePermission(SecurityConstants.CashOperations, SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<CashProposalResponse>>> RejectCashProposalAsync(
        long id, CashProposalActionRequest request, CancellationToken cancellationToken)
    {
        var result = await cashOperations.RejectCashProposalAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashProposalResponse>.FromCode(MessageCode.Success, result));
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
    [AllowWithoutOpenCashSession]
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

    /// <summary>Rejects a pending cash adjustment with a required reason.</summary>
    [HttpPost("adjustments/{id:long}/reject")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<ActionResult<ApiMessageResponse<CashAdjustmentResponse>>> RejectAdjustmentAsync(
        long id, RejectCashAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var adjustment = await cashOperations.RejectAdjustmentAsync(id, request.Reason, cancellationToken);
        return Ok(ApiMessageResponse<CashAdjustmentResponse>.FromCode(MessageCode.Success, adjustment));
    }
}
