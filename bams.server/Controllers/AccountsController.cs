using bams.server.DTO.Accounts;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    private const string GetAccountByIdRouteName = "GetAccountById";
    private readonly IAccountService _accountService;
    private readonly IFixedDepositService _fixedDepositService;

    public AccountsController(
        IAccountService accountService,
        IFixedDepositService fixedDepositService)
    {
        _accountService = accountService;
        _fixedDepositService = fixedDepositService;
    }

    /// <summary>
    /// Gets a forward-only cursor page of account summaries.
    /// </summary>
    [HttpGet]
    [RequirePermission(SecurityConstants.AccountManagement)]
    public async Task<ActionResult<CursorPagedResponse<AccountSummaryResponse>>> GetAccountsAsync(
        [FromQuery] GetAccountsRequest request,
        CancellationToken cancellationToken)
    {
        var accounts = await _accountService.GetAccountsAsync(request, cancellationToken);

        return Ok(accounts);
    }

    /// <summary>
    /// Gets a single account by its unique identifier.
    /// </summary>
    [HttpGet("{id:long}", Name = GetAccountByIdRouteName)]
    [RequirePermission(SecurityConstants.AccountManagement)]
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
    [RequirePermission(SecurityConstants.AccountManagement)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiMessageResponse<AccountResponse>>> CreateAccountAsync(
        [FromForm] CreateAccountRequest request,
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

    /// <summary>
    /// Changes an account's status and records the acting user.
    /// </summary>
    [HttpPatch("{id:long}/status")]
    [RequirePermission("account_management")]
    public async Task<ActionResult<ApiMessageResponse<AccountResponse>>> UpdateAccountStatusAsync(
        long id,
        [FromBody] UpdateAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        var account = await _accountService.UpdateAccountStatusAsync(
            id,
            request.Status,
            request.Reason,
            cancellationToken);
        var response = ApiMessageResponse<AccountResponse>.FromCode(
            MessageCode.AccountStatusUpdatedSuccessfully,
            account);

        return Ok(response);
    }

    /// <summary>
    /// Applies a positive or negative adjustment to an account's balance.
    /// </summary>
    [HttpPatch("{id:long}/balance")]
    [RequirePermission("account_management")]
    public async Task<ActionResult<ApiMessageResponse<AccountResponse>>> UpdateAccountBalanceAsync(
        long id,
        [FromBody] UpdateAccountBalanceRequest request,
        CancellationToken cancellationToken)
    {
        var account = await _accountService.UpdateAccountBalanceAsync(
            id,
            request.Amount,
            cancellationToken);
        var response = ApiMessageResponse<AccountResponse>.FromCode(
            MessageCode.AccountBalanceUpdatedSuccessfully,
            account);

        return Ok(response);
    }

    /// <summary>
    /// Updates the editable holder details of an existing joint account.
    /// </summary>
    [HttpPut("{id:long}/holders")]
    [RequirePermission("account_management")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<AccountHolderResponse>>>> UpdateAccountHoldersAsync(
        long id,
        [FromBody] UpdateAccountHoldersRequest request,
        CancellationToken cancellationToken)
    {
        var holders = await _accountService.UpdateHoldersOfAccountAsync(
            id,
            request,
            cancellationToken);
        var response = ApiMessageResponse<IReadOnlyList<AccountHolderResponse>>.FromCode(
            MessageCode.AccountHoldersUpdatedSuccessfully,
            holders);

        return Ok(response);
    }

    /// <summary>
    /// Updates the editable lifecycle details of an existing fixed deposit.
    /// </summary>
    [HttpPatch("fixed-deposits/{fixedDepositId:long}")]
    [RequirePermission("account_management")]
    public async Task<ActionResult<ApiMessageResponse<FixedDepositResponse>>> UpdateFixedDepositAsync(
        long fixedDepositId,
        [FromBody] UpdateFixedDepositRequest request,
        CancellationToken cancellationToken)
    {
        var fixedDeposit = await _fixedDepositService.UpdateFixedDepositAsync(
            fixedDepositId,
            request,
            cancellationToken);
        var response = ApiMessageResponse<FixedDepositResponse>.FromCode(
            MessageCode.FixedDepositUpdatedSuccessfully,
            fixedDeposit);

        return Ok(response);
    }

}
