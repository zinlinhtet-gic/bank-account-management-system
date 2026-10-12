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
    [HttpGet("recipients")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<CashHandoffRecipientResponse>>>> GetRecipientsAsync(CancellationToken cancellationToken)
    {
        var recipients = await cashOperations.GetHandoffRecipientsAsync(cancellationToken);
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

    [HttpPost("{id:long}/accept")]
    public async Task<ActionResult<ApiMessageResponse<CashHandoffResponse>>> AcceptAsync(long id, CashHandoffActionRequest request, CancellationToken cancellationToken)
    {
        var item = await cashOperations.AcceptCashHandoffAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashHandoffResponse>.FromCode(MessageCode.Success, item));
    }

    [HttpPost("{id:long}/decline")]
    public async Task<ActionResult<ApiMessageResponse<CashHandoffResponse>>> DeclineAsync(long id, CashHandoffActionRequest request, CancellationToken cancellationToken)
    {
        var item = await cashOperations.DeclineCashHandoffAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashHandoffResponse>.FromCode(MessageCode.Success, item));
    }

    [HttpPut("{id:long}/recipient")]
    public async Task<ActionResult<ApiMessageResponse<CashHandoffResponse>>> ReassignAsync(long id, ReassignCashHandoffRequest request, CancellationToken cancellationToken)
    {
        var item = await cashOperations.ReassignCashHandoffAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<CashHandoffResponse>.FromCode(MessageCode.Success, item));
    }
}
