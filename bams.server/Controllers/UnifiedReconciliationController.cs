using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/reconciliation")]
public sealed class UnifiedReconciliationController(UnifiedReconciliationService checks,
    ReconciliationExclusionService exclusions, CashCorrectionService cash) : ControllerBase
{
    [HttpGet("exceptions/{id:long}/detail")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.ReconciliationInvestigation, SecurityConstants.EndOfDayApproval)]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) =>
        Ok(ApiMessageResponse<ReconciliationExceptionDetailResponse>.FromCode(MessageCode.Success, await checks.DetailAsync(id, ct)));

    [HttpGet("account-options")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> AccountOptions([FromQuery] string? search, CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<ReconciliationAccountOptionResponse>>.FromCode(MessageCode.Success, await checks.AccountOptionsAsync(search, ct)));

    [HttpGet("staff-options")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.TransactionCorrectionApproval)]
    public async Task<IActionResult> Staff(CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<ReconciliationStaffOptionResponse>>.FromCode(MessageCode.Success, await checks.StaffAsync(ct)));

    [HttpGet("audit/{date}")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.Audit, SecurityConstants.EndOfDayApproval)]
    public async Task<IActionResult> Audit(DateOnly date, CancellationToken ct) =>
        Ok(ApiMessageResponse<EndOfDayAuditReport?>.FromCode(MessageCode.Success, await checks.AuditAsync(date, ct)));

    [HttpPost("checks")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.Audit, SecurityConstants.EndOfDayApproval)]
    public async Task<IActionResult> Run(UnifiedReconciliationRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<UnifiedReconciliationResponse>.FromCode(MessageCode.Success, await checks.RunAsync(request, ct)));

    [HttpGet("exceptions")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.Audit, SecurityConstants.EndOfDayApproval)]
    public async Task<IActionResult> Exceptions([FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate, CancellationToken ct) =>
        Ok(ApiMessageResponse<UnifiedReconciliationResponse>.FromCode(MessageCode.Success, await checks.ReadAsync(fromDate, toDate, 0, ct)));

    [HttpGet("tasks/mine")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> MyTasks(CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<UnifiedExceptionRow>>.FromCode(MessageCode.Success, await checks.MyTasksAsync(ct)));

    [HttpPost("exceptions/{id:long}/assign")]
    [RequirePermission(SecurityConstants.Accounting, SecurityConstants.TransactionCorrectionApproval)]
    public async Task<IActionResult> Assign(long id, AssignReconciliationRequest request, CancellationToken ct)
    {
        await checks.AssignAsync(id, request, ct);
        return Ok(ApiMessageResponse<bool>.FromCode(MessageCode.Success, true));
    }

    [HttpPost("exceptions/{id:long}/force-pass")]
    [RequirePermission(SecurityConstants.TransactionCorrectionApproval)]
    public async Task<IActionResult> ForcePass(long id, ForcePassRequest request, CancellationToken ct)
    {
        await exclusions.ForcePassAsync(id, request.Reason, ct);
        return Ok(ApiMessageResponse<bool>.FromCode(MessageCode.Success, true));
    }

    [HttpGet("cash/discrepancies")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public IActionResult CashDefinitions() =>
        Ok(ApiMessageResponse<IReadOnlyList<CashDiscrepancyDefinition>>.FromCode(MessageCode.Success, CashCorrectionService.Definitions));

    [HttpPost("cash/exceptions/{id:long}/preview")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> CashPreview(long id, CashCorrectionInputs input, CancellationToken ct) =>
        Ok(ApiMessageResponse<CashCorrectionPreview>.FromCode(MessageCode.Success, await cash.PreviewAsync(id, input, ct)));

    [HttpPost("cash/exceptions/{id:long}/proposals")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> CashSubmit(long id, SubmitCashCorrectionRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<CashCorrectionProposalResponse>.FromCode(MessageCode.Success,
            await cash.SubmitAsync(id, request.Inputs, request.ExpectedPreviewHash, ct)));

    [HttpGet("cash/exceptions/{id:long}/sources")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<IActionResult> CashSources(long id, [FromQuery] string? search, CancellationToken ct) =>
        Ok(ApiMessageResponse<CashSourceCatalog>.FromCode(MessageCode.Success, await cash.SourcesAsync(id, search, ct)));

    [HttpGet("cash/proposals/{id:long}/evidence")]
    [RequirePermission(SecurityConstants.TransactionCorrectionApproval)]
    public async Task<IActionResult> CashEvidence(long id, CancellationToken ct) =>
        Ok(ApiMessageResponse<CashProposalEvidence>.FromCode(MessageCode.Success, await cash.ProposalEvidenceAsync(id, ct)));

    [HttpGet("cash/proposals/pending")]
    [RequirePermission(SecurityConstants.TransactionCorrectionApproval)]
    public async Task<IActionResult> PendingCash(CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<CashCorrectionProposalResponse>>.FromCode(MessageCode.Success, await cash.PendingAsync(ct)));

    [HttpPost("cash/proposals/{id:long}/review")]
    [RequirePermission(SecurityConstants.TransactionCorrectionApproval)]
    public async Task<IActionResult> ReviewCash(long id, ReviewCorrectionRequest request, CancellationToken ct) =>
        Ok(ApiMessageResponse<CashCorrectionProposalResponse>.FromCode(MessageCode.Success, await cash.ReviewAsync(id, request, ct)));

    [HttpGet("verification")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation, SecurityConstants.TransactionCorrectionApproval)]
    public async Task<IActionResult> VerificationTasks(CancellationToken ct) =>
        Ok(ApiMessageResponse<IReadOnlyList<ReconciliationVerificationTask>>.FromCode(MessageCode.Success,
            await checks.VerificationTasksAsync(ct)));

    [HttpPost("cash/proposals/{id:long}/verify")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation, SecurityConstants.TransactionCorrectionApproval)]
    public async Task<IActionResult> VerifyCash(long id, CancellationToken ct) =>
        Ok(ApiMessageResponse<CashCorrectionProposalResponse>.FromCode(MessageCode.Success, await cash.VerifyAsync(id, ct)));

    [HttpPost("vault/final-count")]
    [RequirePermission(SecurityConstants.EndOfDayApproval)]
    public async Task<IActionResult> FinalVaultCount(FinalVaultCountRequest request, CancellationToken ct)
    {
        await checks.FinalVaultCountAsync(request, ct);
        return Ok(ApiMessageResponse<bool>.FromCode(MessageCode.Success, true));
    }
}
