using bams.server.Models.Security;

namespace bams.server.Models.Accounting;

/// <summary>Business-date cash position for a teller or vault.</summary>
public sealed class CashPositionSession
{
    public long Id { get; set; }
    public string PositionType { get; set; } = "Teller";
    public long? TellerId { get; set; }
    public User? Teller { get; set; }
    public DateOnly BusinessDate { get; set; }
    public decimal OpeningCash { get; set; }
    public decimal ExpectedClosingCash { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime OpenedAtUtc { get; set; }
    public long OpenedBy { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public long? ClosedBy { get; set; }
}
