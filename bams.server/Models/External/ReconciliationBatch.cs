using bams.server.Models.Security;

namespace bams.server.Models.External;

public sealed class ReconciliationBatch
{
    public long Id { get; set; }

    public DateOnly ReconciliationDate { get; set; }

    public string ReconciliationType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public decimal TotalDebit { get; set; }

    public decimal TotalCredit { get; set; }

    public decimal Difference { get; set; }

    public long PerformedBy { get; set; }

    public User? PerformedByUser { get; set; }

    public DateTime PerformedAt { get; set; }
}
