using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Models.Accounting;
using bams.server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Controllers;

[ApiController]
[Route("api/accounting/reconciliation/workflow")]
[RequirePermission(SecurityConstants.Accounting, SecurityConstants.ReconciliationInvestigation, SecurityConstants.TransactionCorrectionApproval)]
public sealed class ReconciliationWorkflowController(ReconciliationCorrectionService corrections,
    CorrectionPlanningService planner, ReconciliationCheckService checks, ReconciliationProjectionService projections,
    ReconciliationReadService reader) : ControllerBase
{
    // Definitions drive the case-specific proposal form.
    [HttpGet("discrepancies")]
    public IActionResult Definitions() => Ok(ApiMessageResponse<IReadOnlyList<DiscrepancyDefinition>>.FromCode(MessageCode.Success, ReconciliationReadService.Definitions));

    [HttpPost("checks")]
    public async Task<IActionResult> Check(AccountReconciliationRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<object>.FromCode(MessageCode.Success, await checks.RunAsync(request, ct)));

    [HttpPost("exceptions/{id:long}/investigation")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> Assign(long id, InvestigationRequest request, CancellationToken ct)
    { await corrections.AssignAsync(id, request, ct); return Ok(ApiMessageResponse<bool>.FromCode(MessageCode.Success, true)); }

    [HttpPost("exceptions/{id:long}/preview")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> Preview(long id, CorrectionInputs input, CancellationToken ct) =>
        Ok(ApiMessageResponse<CorrectionPreview>.FromCode(MessageCode.Success, await planner.PreviewAsync(id, input, ct)));

    [HttpPost("exceptions/{id:long}/proposals")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> Save(long id, SaveCorrectionRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<CorrectionProposalResponse>.FromCode(MessageCode.Success, await corrections.SaveAsync(id, null, request, ct)));

    [HttpPost("exceptions/{id:long}/proposals/{draftId:long}")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> SaveDraft(long id, long draftId, SaveCorrectionRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<CorrectionProposalResponse>.FromCode(MessageCode.Success, await corrections.SaveAsync(id, draftId, request, ct)));

    [HttpGet("proposals")]
    public async Task<IActionResult> List([FromQuery] long? exceptionId, CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<CorrectionProposalResponse>>.FromCode(MessageCode.Success, await corrections.ListAsync(exceptionId, ct)));

    [HttpGet("proposals/{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        Ok(ApiMessageResponse<CorrectionProposalResponse>.FromCode(MessageCode.Success, await corrections.GetAsync(id, ct)));

    [HttpPost("proposals/{id:long}/review")]
    [RequirePermission(SecurityConstants.TransactionCorrectionApproval)]
    public async Task<IActionResult> Review(long id, ReviewCorrectionRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<CorrectionProposalResponse>.FromCode(MessageCode.Success, await corrections.ReviewAsync(id, request, ct)));

    [HttpPost("proposals/{id:long}/verify")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> Verify(long id, CancellationToken ct) =>
        Ok(ApiMessageResponse<CorrectionProposalResponse>.FromCode(MessageCode.Success, await corrections.VerifyAsync(id, ct)));

    [HttpGet("transactions/{id:long}/evidence")]
    public async Task<IActionResult> Evidence(long id, CancellationToken ct) =>
        Ok(ApiMessageResponse<object>.FromCode(MessageCode.Success, await reader.EvidenceAsync(id, ct)));

    [HttpGet("gl-options")]
    public async Task<IActionResult> GlOptions(CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<CorrectionGlOption>>.FromCode(MessageCode.Success, await reader.GlOptionsAsync(ct)));

    [HttpGet("summaries")]
    public async Task<IActionResult> Summaries([FromQuery] DateOnly date, CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<SummaryRevisionResponse>>.FromCode(MessageCode.Success, await reader.SummariesAsync(date, ct)));

    [HttpPost("summaries/{date}/refresh")]
    public async Task<IActionResult> Refresh(DateOnly date, CancellationToken ct)
    { await projections.GenerateProvisionalAsync(date, ct); return Ok(ApiMessageResponse<bool>.FromCode(MessageCode.Success, true)); }
}

