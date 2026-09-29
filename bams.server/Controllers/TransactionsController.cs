using bams.server.Constants;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using bams.server.Utils.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

/// <summary>
/// Transactions. Postings need the <c>transactions</c> permission (officers); reads also accept
/// <c>transaction_history</c> (auditors). Posting endpoints accept an optional <c>Idempotency-Key</c> header:
/// retrying with the same key returns the original result instead of posting again.
/// </summary>
[ApiController]
[Route("api/transactions")]
public sealed class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly IInterbankTransferService _interbankTransferService;
    private readonly INrcTransferService _nrcTransferService;
    private readonly ITransactionQueryService _transactionQueryService;

    public TransactionsController(
        ITransactionService transactionService,
        IInterbankTransferService interbankTransferService,
        INrcTransferService nrcTransferService,
        ITransactionQueryService transactionQueryService)
    {
        _transactionService = transactionService;
        _interbankTransferService = interbankTransferService;
        _nrcTransferService = nrcTransferService;
        _transactionQueryService = transactionQueryService;
    }

    /// <summary>
    /// Lists transactions, newest first. Optional filters: accountId, type, status, from, before, page, pageSize.
    /// </summary>
    [HttpGet]
    [RequirePermission(SecurityConstants.Transactions, SecurityConstants.TransactionHistory)]
    public async Task<ActionResult<ApiMessageResponse<PagedResponse<TransactionSummaryResponse>>>> GetTransactionsAsync(
        [FromQuery] TransactionListQuery query,
        CancellationToken cancellationToken)
    {
        var transactions = await _transactionQueryService.GetTransactionsAsync(query, cancellationToken);

        return Ok(ApiMessageResponse<PagedResponse<TransactionSummaryResponse>>.FromCode(
            MessageCode.Success,
            transactions));
    }

    /// <summary>
    /// Lists the banks an interbank transfer can be sent to.
    /// </summary>
    [HttpGet("other-banks")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<OtherBankResponse>>>> GetOtherBanksAsync(
        CancellationToken cancellationToken)
    {
        var banks = await _transactionQueryService.GetOtherBanksAsync(cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<OtherBankResponse>>.FromCode(MessageCode.Success, banks));
    }

    /// <summary>
    /// Lists the branches where NRC transfers can be collected.
    /// </summary>
    [HttpGet("branches")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<BranchResponse>>>> GetBranchesAsync(
        CancellationToken cancellationToken)
    {
        var branches = await _transactionQueryService.GetBranchesAsync(cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<BranchResponse>>.FromCode(MessageCode.Success, branches));
    }

    /// <summary>
    /// Gets one transaction with its account entries and NRC or interbank detail.
    /// </summary>
    [HttpGet("{id:long}")]
    [RequirePermission(SecurityConstants.Transactions, SecurityConstants.TransactionHistory)]
    public async Task<ActionResult<ApiMessageResponse<TransactionDetailResponse>>> GetTransactionByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var transaction = await _transactionQueryService.GetTransactionByIdAsync(id, cancellationToken);

        return Ok(ApiMessageResponse<TransactionDetailResponse>.FromCode(MessageCode.Success, transaction));
    }

    /// <summary>
    /// Gets an account statement, newest first. Optional filters: from, before, page, pageSize.
    /// </summary>
    [HttpGet("accounts/{accountId:long}/statement")]
    [RequirePermission(SecurityConstants.Transactions, SecurityConstants.TransactionHistory)]
    public async Task<ActionResult<ApiMessageResponse<PagedResponse<AccountStatementLineResponse>>>> GetAccountStatementAsync(
        long accountId,
        [FromQuery] AccountStatementQuery query,
        CancellationToken cancellationToken)
    {
        var statement = await _transactionQueryService.GetAccountStatementAsync(accountId, query, cancellationToken);

        return Ok(ApiMessageResponse<PagedResponse<AccountStatementLineResponse>>.FromCode(
            MessageCode.Success,
            statement));
    }

    /// <summary>
    /// Deposits cash into an account.
    /// </summary>
    [HttpPost("deposit")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> DepositAsync(
        DepositRequest request,
        [FromHeader(Name = TransactionConstants.IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _transactionService.DepositAsync(
            request,
            HttpContext.GetRequestActor(),
            idempotencyKey,
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(MessageCode.TransactionCompletedSuccessfully, response));
    }

    /// <summary>
    /// Withdraws cash from an account.
    /// </summary>
    [HttpPost("withdrawal")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> WithdrawAsync(
        WithdrawalRequest request,
        [FromHeader(Name = TransactionConstants.IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _transactionService.WithdrawAsync(
            request,
            HttpContext.GetRequestActor(),
            idempotencyKey,
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(MessageCode.TransactionCompletedSuccessfully, response));
    }

    /// <summary>
    /// Transfers funds between two accounts in this bank.
    /// </summary>
    [HttpPost("transfer/internal")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> TransferInternallyAsync(
        InternalTransferRequest request,
        [FromHeader(Name = TransactionConstants.IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _transactionService.TransferInternallyAsync(
            request,
            HttpContext.GetRequestActor(),
            idempotencyKey,
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(MessageCode.TransactionCompletedSuccessfully, response));
    }

    /// <summary>
    /// Submits a transfer to an account at another bank; it stays pending until the gateway result is recorded.
    /// </summary>
    [HttpPost("transfer/interbank")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> TransferInterbankAsync(
        InterbankTransferRequest request,
        [FromHeader(Name = TransactionConstants.IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _interbankTransferService.CreateTransferAsync(
            request,
            HttpContext.GetRequestActor(),
            idempotencyKey,
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(MessageCode.TransactionSubmittedSuccessfully, response));
    }

    /// <summary>
    /// Records that the gateway settled a pending interbank transfer. Officers or managers (operation) may do this.
    /// </summary>
    [HttpPost("transfer/interbank/{id:long}/complete")]
    [RequirePermission(SecurityConstants.Transactions, SecurityConstants.Operation)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> CompleteInterbankTransferAsync(
        long id,
        InterbankSettlementRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _interbankTransferService.CompleteTransferAsync(
            id,
            request,
            HttpContext.GetRequestActor(),
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(
            MessageCode.InterbankTransferSettledSuccessfully,
            response));
    }

    /// <summary>
    /// Records that the gateway rejected a pending interbank transfer and refunds the sender.
    /// Returns the refund (Reversal) transaction.
    /// </summary>
    [HttpPost("transfer/interbank/{id:long}/fail")]
    [RequirePermission(SecurityConstants.Transactions, SecurityConstants.Operation)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> FailInterbankTransferAsync(
        long id,
        InterbankFailureRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _interbankTransferService.FailTransferAsync(
            id,
            request,
            HttpContext.GetRequestActor(),
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(MessageCode.TransactionRefundedSuccessfully, response));
    }

    /// <summary>
    /// Creates a pending NRC transfer and returns its one-time pickup code.
    /// </summary>
    [HttpPost("transfer/nrc")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> CreateNrcTransferAsync(
        NrcTransferRequest request,
        [FromHeader(Name = TransactionConstants.IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _nrcTransferService.CreateTransferAsync(
            request,
            HttpContext.GetRequestActor(),
            idempotencyKey,
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(MessageCode.TransactionSubmittedSuccessfully, response));
    }

    /// <summary>
    /// Records that the other bank paid out an NRC transfer collected there.
    /// </summary>
    [HttpPost("transfer/nrc/{id:long}/paid-out")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> RecordNrcOtherBankPayoutAsync(
        long id,
        NrcPayoutRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _nrcTransferService.RecordOtherBankPayoutAsync(
            id,
            request,
            HttpContext.GetRequestActor(),
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(MessageCode.TransactionCompletedSuccessfully, response));
    }

    /// <summary>
    /// Cancels a pending NRC transfer and refunds the sender. Returns the refund (Reversal) transaction.
    /// </summary>
    [HttpPost("transfer/nrc/{id:long}/cancel")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> CancelNrcTransferAsync(
        long id,
        NrcCancelRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _nrcTransferService.CancelTransferAsync(
            id,
            request,
            HttpContext.GetRequestActor(),
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(MessageCode.TransactionRefundedSuccessfully, response));
    }

    /// <summary>
    /// Pays out an NRC transfer at one of our branches after the pickup code is verified (cash or into an account).
    /// </summary>
    [HttpPost("nrc-pickup")]
    [RequirePermission(SecurityConstants.Transactions)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> CompleteNrcPickupAsync(
        NrcPickupRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _nrcTransferService.CompletePickupAsync(
            request,
            HttpContext.GetRequestActor(),
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(MessageCode.TransactionCompletedSuccessfully, response));
    }
}
