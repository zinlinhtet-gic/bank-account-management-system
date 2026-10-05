using bams.server.Middlewares;
using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/accounting")]
// The general ledger and its summaries are restricted to users holding the accounting permission.
[RequirePermission(SecurityConstants.Accounting)]
public sealed class AccountingController : ControllerBase
{
    private readonly IAccountingReportService _accountingReportService;
    private readonly IAccountReconciliationService _reconciliationService;

    public AccountingController(
        IAccountingReportService accountingReportService,
        IAccountReconciliationService reconciliationService)
    {
        _accountingReportService = accountingReportService;
        _reconciliationService = reconciliationService;
    }

    /// <summary>Runs account versus customer-ledger reconciliation for a date cutoff.</summary>
    [HttpPost("reconciliation/accounts")]
    public async Task<ActionResult<ApiMessageResponse<AccountReconciliationRunResponse>>> ReconcileAccountsAsync(
        AccountReconciliationRequest request, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.ReconcileAccountsAsync(request, cancellationToken);
        return Ok(ApiMessageResponse<AccountReconciliationRunResponse>.FromCode(MessageCode.Success, result));
    }

    /// <summary>Lists durable reconciliation exceptions.</summary>
    [HttpGet("reconciliation/exceptions")]
    public async Task<ActionResult<ApiMessageResponse<PagedResponse<ReconciliationExceptionResponse>>>> GetReconciliationExceptionsAsync(
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] string? status,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.GetExceptionsAsync(fromDate, toDate, status, page, pageSize, cancellationToken);
        return Ok(ApiMessageResponse<PagedResponse<ReconciliationExceptionResponse>>.FromCode(MessageCode.Success, result));
    }

    [HttpGet("reconciliation/exceptions/{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<ReconciliationExceptionDetailResponse>>> GetReconciliationExceptionByIdAsync(
        long id, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.GetExceptionByIdAsync(id, cancellationToken);
        return Ok(ApiMessageResponse<ReconciliationExceptionDetailResponse>.FromCode(MessageCode.Success, result));
    }

    /// <summary>Updates exception assignment and investigation notes; resolution is driven by a successful rerun.</summary>
    [HttpPatch("reconciliation/exceptions/{id:long}")]
    [RequirePermission(SecurityConstants.ReconciliationInvestigation)]
    public async Task<ActionResult<ApiMessageResponse<ReconciliationExceptionResponse>>> UpdateReconciliationExceptionAsync(
        long id, UpdateReconciliationExceptionRequest request, CancellationToken cancellationToken)
    {
        var result = await _reconciliationService.UpdateExceptionAsync(id, request, cancellationToken);
        return Ok(ApiMessageResponse<ReconciliationExceptionResponse>.FromCode(MessageCode.Success, result));
    }

    // Returns all general-ledger accounts.
    [HttpGet("gl-accounts")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<GlAccountResponse>>>>
        GetGlAccountsAsync(
            CancellationToken cancellationToken)
    {
        var accounts =
            await _accountingReportService.GetGlAccountsAsync(
                cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<GlAccountResponse>>.FromCode(
            MessageCode.Success,
            accounts));
    }

    // Returns a general-ledger account by its unique identifier.
    [HttpGet("gl-accounts/{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<GlAccountResponse>>>
        GetGlAccountByIdAsync(
            long id,
            CancellationToken cancellationToken)
    {
        var account =
            await _accountingReportService.GetGlAccountByIdAsync(
                id,
                cancellationToken);

        return Ok(ApiMessageResponse<GlAccountResponse>.FromCode(
            MessageCode.Success,
            account));
    }

    // Returns GL account metadata and complete journal lines for related transactions.
    [HttpGet("gl-accounts/{id:long}/detail")]
    public async Task<ActionResult<ApiMessageResponse<GlAccountDetailResponse>>>
        GetGlAccountDetailAsync(
            long id,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            CancellationToken cancellationToken)
    {
        var detail = await _accountingReportService.GetGlAccountDetailAsync(id, page, pageSize, cancellationToken);
        return Ok(ApiMessageResponse<GlAccountDetailResponse>.FromCode(MessageCode.Success, detail));
    }

    // Returns daily accounting summaries for the requested date.
    [HttpGet("daily-summaries")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<DailySummaryResponse>>>>
        GetDailySummariesAsync(
            [FromQuery] DateOnly date,
            [FromQuery] long? glAccountId,
            CancellationToken cancellationToken)
    {
        var summaries =
            await _accountingReportService.GetDailySummariesAsync(
                date,
                glAccountId,
                cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<DailySummaryResponse>>.FromCode(
            MessageCode.Success,
            summaries));
    }

    // Returns monthly accounting summaries for the requested period.
    [HttpGet("monthly-summaries")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<MonthlySummaryResponse>>>>
        GetMonthlySummariesAsync(
            [FromQuery] int year,
            [FromQuery] int month,
            [FromQuery] long? glAccountId,
            CancellationToken cancellationToken)
    {
        var summaries =
            await _accountingReportService.GetMonthlySummariesAsync(
                year,
                month,
                glAccountId,
                cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<MonthlySummaryResponse>>.FromCode(
            MessageCode.Success,
            summaries));
    }

    [HttpGet("entries")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<AccountingEntryResponse>>>>
        GetAccountingEntriesAsync(
            [FromQuery] DateOnly? fromDate,
            [FromQuery] DateOnly? toDate,
            [FromQuery] long? glAccountId,
            [FromQuery] EntryType? entryType,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            CancellationToken cancellationToken
        )
    {
        var entries = await _accountingReportService.GetAccountingEntriesAsync(
            fromDate,
            toDate,
            glAccountId,
            entryType,
            page,
            pageSize,
            cancellationToken
        );
        return Ok(ApiMessageResponse<PagedResponse<AccountingEntryResponse>>.FromCode(
            MessageCode.Success,entries
        ));
    }
}
