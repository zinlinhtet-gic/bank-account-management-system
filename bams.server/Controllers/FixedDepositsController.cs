using bams.server.Constants;
using bams.server.DTO.Accounts;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using bams.server.Utils.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

/// <summary>
/// Breaking fixed deposits before maturity. Viewing the quote needs <c>operation</c>; breaking a deposit needs
/// <c>fixed_deposit_early_withdrawal</c> (managers). Maturity payouts run as the <c>fixed-deposit-maturity</c> job.
/// The withdrawal accepts an optional <c>Idempotency-Key</c> header so a retried request is not paid twice.
/// </summary>
[ApiController]
[Route("api/fixed-deposits")]
public sealed class FixedDepositsController : ControllerBase
{
    private readonly IFixedDepositPayoutService _payoutService;

    public FixedDepositsController(IFixedDepositPayoutService payoutService)
    {
        _payoutService = payoutService;
    }

    /// <summary>
    /// Shows what breaking the fixed deposit today would pay: interest earned, interest forfeited and the net payout.
    /// </summary>
    [HttpGet("{id:long}/early-withdrawal-quote")]
    [RequirePermission(SecurityConstants.Operation, SecurityConstants.FixedDepositEarlyWithdrawal)]
    public async Task<ActionResult<ApiMessageResponse<FixedDepositEarlyWithdrawalQuoteResponse>>> GetEarlyWithdrawalQuoteAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var quote = await _payoutService.GetEarlyWithdrawalQuoteAsync(id, cancellationToken);

        return Ok(ApiMessageResponse<FixedDepositEarlyWithdrawalQuoteResponse>.FromCode(MessageCode.Success, quote));
    }

    /// <summary>
    /// Breaks the fixed deposit and pays it to its payout account, less the forfeited interest.
    /// </summary>
    [HttpPost("{id:long}/early-withdrawal")]
    [RequirePermission(SecurityConstants.FixedDepositEarlyWithdrawal)]
    public async Task<ActionResult<ApiMessageResponse<TransactionResponse>>> WithdrawFixedDepositEarlyAsync(
        long id,
        EarlyWithdrawFixedDepositRequest request,
        [FromHeader(Name = TransactionConstants.IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _payoutService.WithdrawFixedDepositEarlyAsync(
            id,
            request,
            HttpContext.GetRequestActor(),
            idempotencyKey,
            cancellationToken);

        return Ok(ApiMessageResponse<TransactionResponse>.FromCode(
            MessageCode.FixedDepositWithdrawnEarlySuccessfully, response));
    }
}
