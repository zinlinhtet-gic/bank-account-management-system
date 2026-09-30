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

    public AccountingController(
        IAccountingReportService accountingReportService)
    {
        _accountingReportService = accountingReportService;
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
