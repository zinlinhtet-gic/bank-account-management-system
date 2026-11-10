using bams.server.DTO.Accounts;
using bams.server.DTO.Common;
using bams.server.DTO.Transactions;

namespace bams.server.Services.Interfaces;

/// <summary>
/// Pays fixed deposits out: early withdrawal on request. Maturity payouts run as a scheduled job on
/// <see cref="FixedDepositPayoutService"/>.
/// </summary>
public interface IFixedDepositPayoutService
{
    /// <summary>
    /// Calculates what breaking the fixed deposit today would pay, without posting anything.
    /// </summary>
    Task<FixedDepositEarlyWithdrawalQuoteResponse> GetEarlyWithdrawalQuoteAsync(
        long fixedDepositId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Breaks an active fixed deposit before maturity: credits its interest to date, forfeits interest above the
    /// early-withdrawal rate, pays the rest to the payout account, and closes the deposit and its account.
    /// A repeated <paramref name="idempotencyKey"/> returns the original result.
    /// </summary>
    Task<TransactionResponse> WithdrawFixedDepositEarlyAsync(
        long fixedDepositId,
        EarlyWithdrawFixedDepositRequest request,
        RequestActor actor,
        string? idempotencyKey,
        CancellationToken cancellationToken);
}
