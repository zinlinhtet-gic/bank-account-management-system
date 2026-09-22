namespace bams.server.Models.Products;

public sealed class AccountType
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Category { get; set; }

    public decimal MinimumOpeningBalance { get; set; }

    public decimal MinimumMaintainedBalance { get; set; }

    public decimal? DailyTransactionLimit { get; set; }

    public decimal? MonthlyTransactionLimit { get; set; }

    public bool AllowWithdrawal { get; set; }

    public bool AllowTransfer { get; set; }

    public bool AllowPartialWithdrawal { get; set; }

    public string Status { get; set; } = string.Empty;

    public long? RequiredProductId { get; set; } = null;
    public AccountType? RequiredProduct { get; set; }

    public bool IsFixedDeposit { get; set; } = false;
}
