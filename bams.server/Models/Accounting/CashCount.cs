using bams.server.Models.Security;

namespace bams.server.Models.Accounting;

/// <summary>Physical cash count submitted against a closed teller or vault session.</summary>
public sealed class CashCount
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public CashPositionSession? Session { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Difference { get; set; }
    public string? Notes { get; set; }
    public long CountedBy { get; set; }
    public User? CountedByUser { get; set; }
    public DateTime CountedAtUtc { get; set; }
    public string? IdempotencyKey { get; set; }
}
