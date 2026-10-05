using bams.server.Models.Security;

namespace bams.server.Models.Accounting;

/// <summary>Append-only audit trail for reconciliation exception status and investigation changes.</summary>
public sealed class ReconciliationExceptionHistory
{
    public long Id { get; set; }
    public long ExceptionId { get; set; }
    public ReconciliationException? Exception { get; set; }
    public string OldStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public string? Note { get; set; }
    public long ActorId { get; set; }
    public User? Actor { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
