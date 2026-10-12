using bams.server.Models.Security;

namespace bams.server.Models.Accounting;

/// <summary>Append-only record of cash handoff creation, receipt, decline, and reassignment.</summary>
public sealed class CashHandoffHistory
{
    public long Id { get; set; }
    public long CashHandoffId { get; set; }
    public CashHandoff? CashHandoff { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public long? PreviousRecipientId { get; set; }
    public long? RecipientId { get; set; }
    public string? Note { get; set; }
    public long ActorId { get; set; }
    public User? Actor { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
