using bams.server.Models.Accounts;
using bams.server.Models.Products;
using bams.server.Models.Transactions;

namespace bams.server.Models.InterestFees;

public sealed class FeeAccrual
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public Account? Account { get; set; }

    public long FeeRuleId { get; set; }

    public FeeRule? FeeRule { get; set; }

    public FeeType FeeType { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public decimal Amount { get; set; }

    public decimal TaxAmount { get; set; }

    public FeeAccrualStatus Status { get; set; } = FeeAccrualStatus.Accrued;

    public long? PostedTransactionId { get; set; }

    public Transaction? PostedTransaction { get; set; }

    public DateTime CalculatedAt { get; set; }

    public DateTime? PostedAt { get; set; }
}
