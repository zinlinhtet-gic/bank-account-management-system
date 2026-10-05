using bams.server.Models.Accounts;
using bams.server.Models.Security;
using bams.server.Models.Transactions;

namespace bams.server.Models.Accounting;

/// <summary>Durable investigation record for an account, ledger, or cash discrepancy.</summary>
public sealed class ReconciliationException
{
    public long Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public DateOnly BusinessDate { get; set; }
    public long? AccountId { get; set; }
    public Account? Account { get; set; }
    public long? PositionSessionId { get; set; }
    public CashPositionSession? PositionSession { get; set; }
    public decimal ExpectedAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public decimal Difference { get; set; }
    public string Severity { get; set; } = "Critical";
    public string Status { get; set; } = "Open";
    public long? AssignedTo { get; set; }
    public User? AssignedUser { get; set; }
    public long? RelatedTransactionId { get; set; }
    public Transaction? RelatedTransaction { get; set; }
    public long? CorrectionTransactionId { get; set; }
    public Transaction? CorrectionTransaction { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public long CreatedBy { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public ICollection<ReconciliationExceptionHistory> History { get; set; } = new List<ReconciliationExceptionHistory>();
}
