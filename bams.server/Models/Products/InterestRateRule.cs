namespace bams.server.Models.Products;

public sealed class InterestRateRule
{
    public long Id { get; set; }

    public long AccountTypeId { get; set; }

    public AccountType? AccountType { get; set; }

    public int? TermDays { get; set; }

    public int? TermMonths { get; set; }

    public decimal? BalanceMin { get; set; }

    public decimal? BalanceMax { get; set; }

    public decimal AnnualRate { get; set; }

    public decimal? EarlyWithdrawalRate { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public string Status { get; set; } = string.Empty;
}
