using bams.server.Constants;
using bams.server.DTO.Accounts;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    private const string GetAccountByIdRouteName = "GetAccountById";

    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    /// <summary>
    /// Gets all account summaries.
    /// </summary>
    [HttpGet]
    [RequirePermission("account_management")]
    public async Task<ActionResult<IReadOnlyList<AccountSummaryResponse>>> GetAccountsAsync(
        CancellationToken cancellationToken)
    {
        var accounts = await _accountService.GetAccountsAsync(cancellationToken);

        return Ok(accounts);
    }

    /// <summary>
    /// Gets a single account by its unique identifier.
    /// </summary>
    [HttpGet("{id:long}", Name = GetAccountByIdRouteName)]
    [RequirePermission("account_management")]
    public async Task<ActionResult<AccountResponse>> GetAccountByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var account = await _accountService.GetAccountByIdAsync(id, cancellationToken);

        return Ok(account);
    }

    /// <summary>
    /// Creates a new account from the supplied API request contract.
    /// </summary>
    [HttpPost]
    [RequirePermission("account_management")]
    public async Task<ActionResult<ApiMessageResponse<AccountResponse>>> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var account = await _accountService.CreateAccountAsync(request, cancellationToken);
        var response = ApiMessageResponse<AccountResponse>.FromCode(
            MessageCode.AccountCreatedSuccessfully,
            account);

        return CreatedAtRoute(
            GetAccountByIdRouteName,
            new { id = account.Id },
            response);
    }
}
