using bams.server.Models.Security;

namespace bams.server.Models.Accounting;

/// <summary>Tracks physical cash custody passed from a closed session to another authorized user.</summary>
public sealed class CashHandoff
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public CashPositionSession? Session { get; set; }
    public long CashCountId { get; set; }
    public CashCount? CashCount { get; set; }
    public DateOnly BusinessDate { get; set; }
    public long SenderId { get; set; }
    public User? Sender { get; set; }
    public long RecipientId { get; set; }
    public User? Recipient { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public DateTime? DeclinedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}
