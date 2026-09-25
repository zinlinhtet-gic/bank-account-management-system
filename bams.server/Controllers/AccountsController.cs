using bams.server.DTO.Accounts;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Models.Accounts.Enums;
using bams.server.Services.Interfaces;
using bams.server.Constants;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    private const string GetAccountByIdRouteName = "GetAccountById";
    private readonly IAccountService _accountService;
    private readonly IFixedDepositService _fixedDepositService;
    private readonly ICustomerLookUpService _customerLookUpService;
    private readonly IAccountTransactionService _accountTransactionService;
    private readonly IAccountStatusHistoryService _accountStatusHistoryService;

    public AccountsController(
        IAccountService accountService,
        IFixedDepositService fixedDepositService,
        ICustomerLookUpService customerLookUpService,
        IAccountTransactionService accountTransactionService,
        IAccountStatusHistoryService accountStatusHistoryService)
    {
        _accountService = accountService;
        _fixedDepositService = fixedDepositService;
        _customerLookUpService = customerLookUpService;
        _accountTransactionService = accountTransactionService;
        _accountStatusHistoryService = accountStatusHistoryService;
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

    /// <summary>Looks up an existing customer using their NRC number.</summary>
    [HttpGet("customer-lookup")]
    [RequirePermission(SecurityConstants.AccountManagement)]
    public async Task<ActionResult<CustomerLookupResponse>> GetCustomerByNrcAsync([FromQuery] string nrc, CancellationToken cancellationToken)
        => Ok(await _customerLookUpService.GetCustomerByNrcAsync(nrc, cancellationToken));

    /// <summary>Gets eligible account types, required documents, and accounts owned by the primary holder.</summary>
    [HttpGet("opening-options")]
    [RequirePermission(SecurityConstants.AccountManagement)]
    public async Task<ActionResult<AccountOpeningOptionsResponse>> GetAccountOpeningOptionsAsync(
        [FromQuery] string holderNrc, [FromQuery] string? secondHolderNrc, CancellationToken cancellationToken)
        => Ok(await _accountService.GetAccountOpeningOptionsAsync(holderNrc, secondHolderNrc, cancellationToken));

    /// <summary>Gets transaction entries recorded for one account.</summary>
    [HttpGet("{id:long}/transactions")]
    [RequirePermission(SecurityConstants.AccountManagement)]
    public async Task<ActionResult<IReadOnlyList<AccountTransactionDetailResponse>>> GetAccountTransactionsAsync(long id, CancellationToken cancellationToken)
        => Ok(await _accountTransactionService.GetAccountTransactionsAsync(id, cancellationToken));

    /// <summary>Gets status changes recorded for one account.</summary>
    [HttpGet("{id:long}/status-history")]
    [RequirePermission(SecurityConstants.AccountManagement)]
    public async Task<ActionResult<IReadOnlyList<AccountStatusHistoryResponse>>> GetAccountStatusHistoryAsync(long id, CancellationToken cancellationToken)
        => Ok(await _accountStatusHistoryService.GetAccountStatusHistoryAsync(id, cancellationToken));

    /// <summary>Gets calculated interest accruals recorded for one account.</summary>
    [HttpGet("{id:long}/interest-accruals")]
    [RequirePermission(SecurityConstants.AccountManagement)]
    public async Task<ActionResult<IReadOnlyList<InterestAccrualResponse>>> GetAccountInterestAccrualsAsync(long id, CancellationToken cancellationToken)
        => Ok(await _accountService.GetAccountInterestAccrualsAsync(id, cancellationToken));

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
    /// Freezes an active or dormant account.
    /// </summary>
    [HttpPatch("{id:long}/freeze")]
    [RequirePermission("account_management")]
    public async Task<ActionResult<ApiMessageResponse<AccountResponse>>> FreezeAccountAsync(
        long id,
        [FromBody] UpdateAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        var account = await _accountService.UpdateAccountStatusAsync(
            id,
            AccountStatus.Frozen,
            request.Reason,
            request.Version,
            cancellationToken);
        var response = ApiMessageResponse<AccountResponse>.FromCode(
            MessageCode.AccountStatusUpdatedSuccessfully,
            account);

        return Ok(response);
    }

    /// <summary>
    /// Suspends an active or dormant account.
    /// </summary>
    [HttpPatch("{id:long}/suspend")]
    [RequirePermission("account_management")]
    public async Task<ActionResult<ApiMessageResponse<AccountResponse>>> SuspendAccountAsync(
        long id,
        [FromBody] UpdateAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        var account = await _accountService.UpdateAccountStatusAsync(
            id,
            AccountStatus.Suspended,
            request.Reason,
            request.Version,
            cancellationToken);
        var response = ApiMessageResponse<AccountResponse>.FromCode(
            MessageCode.AccountStatusUpdatedSuccessfully,
            account);

        return Ok(response);
    }

    /// <summary>
    /// Reactivates a dormant, suspended, or frozen account.
    /// </summary>
    [HttpPatch("{id:long}/reactivate")]
    [RequirePermission("account_management")]
    public async Task<ActionResult<ApiMessageResponse<AccountResponse>>> ReactivateAccountAsync(
        long id,
        [FromBody] UpdateAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        var account = await _accountService.UpdateAccountStatusAsync(
            id,
            AccountStatus.Active,
            request.Reason,
            request.Version,
            cancellationToken);
        var response = ApiMessageResponse<AccountResponse>.FromCode(
            MessageCode.AccountStatusUpdatedSuccessfully,
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
    /// Updates the payout account and renewal instruction of an existing fixed deposit.
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
