namespace bams.server.Models.Accounting;

/// <summary>Immutable header for one account/ledger reconciliation execution.</summary>
public sealed class AccountReconciliationRun
{
    public long Id { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public long? AccountId { get; set; }
    public long? ScheduledJobExecutionId { get; set; }
    public string Status { get; set; } = "Completed";
    public long PerformedBy { get; set; }
    public DateTime PerformedAtUtc { get; set; }
    public ICollection<AccountReconciliationResult> Results { get; set; } = new List<AccountReconciliationResult>();
}
