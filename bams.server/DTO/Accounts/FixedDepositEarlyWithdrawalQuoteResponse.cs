namespace bams.server.DTO.Accounts;

/// <summary>
/// What breaking a fixed deposit today would pay, before anything is posted.
/// </summary>
/// <param name="Principal">Principal the current term started with.</param>
/// <param name="DaysHeld">Days from the term start to today.</param>
/// <param name="AnnualRate">Contract rate of the term, as an annual percentage.</param>
/// <param name="EarlyWithdrawalRate">Annual percentage paid instead when the deposit is broken early; zero when the rate rule sets none.</param>
/// <param name="InterestEarned">Interest earned this term at the contract rate, including interest not yet credited.</param>
/// <param name="InterestAtEarlyWithdrawalRate">Interest the term earns at the early-withdrawal rate.</param>
/// <param name="InterestForfeited">Interest the customer gives up: earned interest above the early-withdrawal amount.</param>
/// <param name="NetPayout">Amount paid to the payout account: principal plus earned interest minus forfeited interest.</param>
public sealed record FixedDepositEarlyWithdrawalQuoteResponse(
    long FixedDepositId,
    string AccountNo,
    string PayoutAccountNo,
    decimal Principal,
    DateOnly StartDate,
    DateOnly MaturityDate,
    int DaysHeld,
    decimal AnnualRate,
    decimal EarlyWithdrawalRate,
    decimal InterestEarned,
    decimal InterestAtEarlyWithdrawalRate,
    decimal InterestForfeited,
    decimal NetPayout);
