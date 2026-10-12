namespace bams.server.Models.Accounting;

/// <summary>Persisted bank-wide business-date lifecycle; separate from transaction UTC timestamps.</summary>
public sealed class BusinessDate
{
    public long Id { get; set; }
    public DateOnly Date { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime OpenedAtUtc { get; set; }
    public long OpenedBy { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public long? ClosedBy { get; set; }
    public long Version { get; set; } = 1;
}
