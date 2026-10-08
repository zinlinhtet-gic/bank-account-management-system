namespace bams.server.Models.Accounting;

/// <summary>Persisted business-date close attempt and dual-control review state.</summary>
public sealed class EndOfDayRun
{
    public long Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string Status { get; set; } = "Started";
    public string StageSummaryJson { get; set; } = "{}";
    public long PreparedBy { get; set; }
    public DateTime PreparedAtUtc { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public long? ClosedBy { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? OverrideReason { get; set; }
    public string? OverrideRequiredUserIdsJson { get; set; }
    public string? OverrideApprovedUserIdsJson { get; set; }
    public string? OverrideApprovalAuditJson { get; set; }
}
