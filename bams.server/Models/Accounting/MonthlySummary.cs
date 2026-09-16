namespace bams.server.Models.Accounting;

public sealed class MonthlySummary
{
    public long Id { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    public long GlAccountId { get; set; }

    public GlAccount? GlAccount { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    public decimal ClosingBalance { get; set; }

    public DateTime GeneratedAt { get; set; }
}
