using bams.server.Models.Accounts;
using bams.server.Models.Products;
using bams.server.Models.Transactions;

namespace bams.server.Models.InterestFees;

public sealed class InterestAccrual
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public Account? Account { get; set; }

    public long InterestRateRuleId { get; set; }

    public InterestRateRule? InterestRateRule { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public decimal CalculationBalance { get; set; }

    public decimal AnnualRate { get; set; }

    public decimal CalculatedAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public long? PostedTransactionId { get; set; }

    public Transaction? PostedTransaction { get; set; }

    public DateTime CalculatedAt { get; set; }

    public DateTime? PostedAt { get; set; }
}
