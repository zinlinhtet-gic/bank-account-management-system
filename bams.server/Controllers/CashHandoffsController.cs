using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/cash-handoffs")]
[RequirePermission(SecurityConstants.CashOperations, SecurityConstants.Audit, SecurityConstants.EndOfDayApproval)]
public sealed class CashHandoffsController(ICashOperationsService cashOperations) : ControllerBase
{
    // Lists eligible Manager recipients for the Auditor-only transfer screen.
    [HttpGet("auditor-return-managers")]
    public async Task<IActionResult> GetAuditorReturnManagersAsync(CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<CashHandoffRecipientResponse>>.FromCode(MessageCode.Success, await cashOperations.GetAuditorReturnManagersAsync(ct)));
    // Lists auditor custody return requests.
    [HttpGet("auditor-returns")]
    public async Task<IActionResult> GetAuditorReturnsAsync([FromQuery] DateOnly? businessDate, CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<AuditorCashReturnResponse>>.FromCode(MessageCode.Success, await cashOperations.GetAuditorReturnsAsync(businessDate, ct)));

    // Submits whole accepted handoffs for manager acknowledgement.
    [AllowWithoutOpenCashSession]
    [HttpPost("auditor-returns")]
    public async Task<IActionResult> CreateAuditorReturnAsync(CreateAuditorCashReturnRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<AuditorCashReturnResponse>.FromCode(MessageCode.Success, await cashOperations.CreateAuditorReturnAsync(request, ct)));

    // Confirms physical receipt into the vault.
    [AllowWithoutOpenCashSession]
    [HttpPost("auditor-returns/{id:long}/accept")]
    public async Task<IActionResult> AcceptAuditorReturnAsync(long id, CashHandoffActionRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<AuditorCashReturnResponse>.FromCode(MessageCode.Success, await cashOperations.DecideAuditorReturnAsync(id, request, true, ct)));

    // Leaves custody with the auditor and releases the selection for resubmission.
    [AllowWithoutOpenCashSession]
    [HttpPost("auditor-returns/{id:long}/decline")]
    public async Task<IActionResult> DeclineAuditorReturnAsync(long id, CashHandoffActionRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<AuditorCashReturnResponse>.FromCode(MessageCode.Success, await cashOperations.DecideAuditorReturnAsync(id, request, false, ct)));
    [HttpGet("recipients")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<CashHandoffRecipientResponse>>>> GetRecipientsAsync(CancellationToken cancellationToken, [FromQuery] long? sourceSessionId = null)
    {
        var recipients = await cashOperations.GetHandoffRecipientsAsync(cancellationToken, sourceSessionId);
        return Ok(ApiMessageResponse<IReadOnlyList<CashHandoffRecipientResponse>>.FromCode(MessageCode.Success, recipients));
    }

    [HttpGet]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<CashHandoffResponse>>>> GetAsync([FromQuery] DateOnly? businessDate, CancellationToken cancellationToken)
    {
        var items = await cashOperations.GetCashHandoffsAsync(businessDate, cancellationToken);
        return Ok(ApiMessageResponse<IReadOnlyList<CashHandoffResponse>>.FromCode(MessageCode.Success, items));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<CashHandoffDetailResponse>>> GetDetailAsync(long id, CancellationToken cancellationToken)
    {
        var item = await cashOperations.GetCashHandoffDetailAsync(id, cancellationToken);
        return Ok(ApiMessageResponse<CashHandoffDetailResponse>.FromCode(MessageCode.Success, item));
    }

    [AllowWithoutOpenCashSession]
    [HttpPost("{id:long}/accept")]
    public async Task<ActionResult<ApiMessageResponse<CashHandoffResponse>>> AcceptAsync(long id, CashHandoffActionRequest request, CancellationToken cancellationToken)
    {
        var item = await cashOperations.AcceptCashHandoffAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashHandoffResponse>.FromCode(MessageCode.Success, item));
    }

    [AllowWithoutOpenCashSession]
    [HttpPost("{id:long}/decline")]
    public async Task<ActionResult<ApiMessageResponse<CashHandoffResponse>>> DeclineAsync(long id, CashHandoffActionRequest request, CancellationToken cancellationToken)
    {
        var item = await cashOperations.DeclineCashHandoffAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashHandoffResponse>.FromCode(MessageCode.Success, item));
    }

    [AllowWithoutOpenCashSession]
    [HttpPut("{id:long}/recipient")]
    public async Task<ActionResult<ApiMessageResponse<CashHandoffResponse>>> ReassignAsync(long id, ReassignCashHandoffRequest request, CancellationToken cancellationToken)
    {
        var item = await cashOperations.ReassignCashHandoffAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashHandoffResponse>.FromCode(MessageCode.Success, item));
    }
}
