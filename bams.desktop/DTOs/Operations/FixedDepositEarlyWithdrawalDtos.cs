namespace bams.desktop.DTOs.Operations;

/// <summary>
/// What breaking a fixed deposit today would pay (GET api/fixed-deposits/{id}/early-withdrawal-quote).
/// Rates are annual percentages; amounts are MMK.
/// </summary>
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

/// <summary>Body of POST api/fixed-deposits/{id}/early-withdrawal.</summary>
public sealed record EarlyWithdrawFixedDepositRequest(string? Reason);
