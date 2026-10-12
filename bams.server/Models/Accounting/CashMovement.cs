using bams.server.Constants;
using bams.server.Models.Security;
using bams.server.Models.Transactions;

namespace bams.server.Models.Accounting;

/// <summary>Immutable cash movement against a teller or vault session.</summary>
public sealed class CashMovement
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public CashPositionSession? Session { get; set; }
    public long? DestinationSessionId { get; set; }
    public CashPositionSession? DestinationSession { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = OperationsConstants.CashMovementApproved;
    public decimal Amount { get; set; }
    public long? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
    public long? CorrectionTransactionId { get; set; }
    public Transaction? CorrectionTransaction { get; set; }
    public long ActorId { get; set; }
    public User? Actor { get; set; }
    public long? ApprovedBy { get; set; }
    public User? Approver { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Note { get; set; }
}
