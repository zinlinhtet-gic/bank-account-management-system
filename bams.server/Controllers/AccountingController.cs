using bams.server.DTO.Accounting;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/accounting")]
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
    public async Task<ActionResult<IReadOnlyList<GlAccountResponse>>>
        GetGlAccountsAsync(
            CancellationToken cancellationToken)
    {
        var accounts =
            await _accountingReportService.GetGlAccountsAsync(
                cancellationToken);

        return Ok(accounts);
    }

    // Returns a general-ledger account by its unique identifier.
    [HttpGet("gl-accounts/{id:long}")]
    public async Task<ActionResult<GlAccountResponse>>
        GetGlAccountByIdAsync(
            long id,
            CancellationToken cancellationToken)
    {
        var account =
            await _accountingReportService.GetGlAccountByIdAsync(
                id,
                cancellationToken);

        return Ok(account);
    }

    // Returns daily accounting summaries for the requested date.
    [HttpGet("daily-summaries")]
    public async Task<ActionResult<IReadOnlyList<DailySummaryResponse>>>
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

        return Ok(summaries);
    }

    // Returns monthly accounting summaries for the requested period.
    [HttpGet("monthly-summaries")]
    public async Task<ActionResult<IReadOnlyList<MonthlySummaryResponse>>>
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

        return Ok(summaries);
    }
}