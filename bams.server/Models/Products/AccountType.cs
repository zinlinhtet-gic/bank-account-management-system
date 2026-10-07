namespace bams.server.Models.Products;

public sealed class AccountType
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public AccountTypeCategory Category { get; set; }

    public decimal MinimumOpeningBalance { get; set; }

    public decimal MinimumMaintainedBalance { get; set; }

    public decimal? DailyTransactionLimit { get; set; }

    public decimal? MonthlyTransactionLimit { get; set; }

    // Customer debits allowed per Myanmar business week (Monday to Sunday); null means no weekly limit.
    public decimal? WeeklyTransactionLimit { get; set; }

    // Cash withdrawals allowed per business day, on top of the overall daily limit; null means no extra limit.
    public decimal? DailyWithdrawalLimit { get; set; }

    // Smallest cash deposit accepted; null means any positive amount.
    public decimal? MinimumDepositAmount { get; set; }

    // Smallest cash withdrawal accepted; null means any positive amount.
    public decimal? MinimumWithdrawalAmount { get; set; }

    public bool AllowDeposit { get; set; }

    public bool AllowWithdrawal { get; set; }

    public bool AllowTransfer { get; set; }

    public bool AllowPartialWithdrawal { get; set; }

    public string Status { get; set; } = string.Empty;

    public long? RequiredProductId { get; set; } = null;
    public AccountType? RequiredProduct { get; set; }

    public bool AllowForeigner { get; set; } = true;

    public bool AllowCitizen { get; set; } = true;

    public int CitizenRequiredRefer { get; set; }

    public int ForeignRequiredRefer { get; set; }
}
