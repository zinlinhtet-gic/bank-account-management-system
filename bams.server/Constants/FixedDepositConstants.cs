namespace bams.server.Constants;

/// <summary>
/// Fixed-deposit payout: the maturity job, early withdrawal, and the texts written on their postings.
/// </summary>
public static class FixedDepositConstants
{
    public const string MaturityJobKey = "fixed-deposit-maturity";
    public const string MaturityJobDisplayName = "Fixed Deposit Maturity";

    // Maturity is checked hourly: postings need an open business date, which a manager opens during the morning,
    // so a deposit maturing today is paid within an hour of the day being opened.
    public static readonly TimeSpan MaturityJobInterval = TimeSpan.FromHours(1);

    // Deposits handled per maturity run; the rest wait for the next run.
    public const int MaturityBatchSize = 200;

    public const string MaturityInterestCreditDescription = "Fixed-deposit interest credited at maturity";
    public const string EarlyWithdrawalInterestCreditDescription = "Fixed-deposit interest credited before early withdrawal";
    public const string MaturityPayoutDescription = "Fixed deposit maturity payout";
    public const string EarlyWithdrawalPayoutDescription = "Fixed deposit early withdrawal";
    public const string ForfeitedInterestEntryDescription = "Interest forfeited for early withdrawal";
    public const string MaturityClosureReason = "Fixed deposit paid out at maturity";
    public const string EarlyWithdrawalClosureReason = "Fixed deposit withdrawn before maturity";
}
