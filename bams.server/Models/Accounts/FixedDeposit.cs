using bams.server.Models.Products;
using bams.server.Models.Accounts.Enums;

namespace bams.server.Models.Accounts;

public sealed class FixedDeposit
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public Account? Account { get; set; }

    public decimal PrincipalAmount { get; set; }

    public long InterestRateRuleId { get; set; }

    public InterestRateRule? InterestRateRule { get; set; }

    public decimal AppliedAnnualRate { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly MaturityDate { get; set; }

    public int? TermDays { get; set; }

    public int? TermMonths { get; set; }

    public RenewalInstruction RenewalInstruction { get; set; }

    public long PayoutAccountId { get; set; }

    public Account? PayoutAccount { get; set; }

    public string Status { get; set; } = string.Empty;

    public decimal OriginalPrincipal { get; set; }

    public decimal CurrentPrincipal { get; set; }

    public bool CalculateFromCurrent { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
