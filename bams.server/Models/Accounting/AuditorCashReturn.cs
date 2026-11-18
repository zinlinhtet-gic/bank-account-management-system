namespace bams.server.Models.Accounting;

/// <summary>Groups whole handoffs physically held by an auditor for manager receipt.</summary>
public sealed class AuditorCashReturn
{
    public long Id { get; set; }
    public DateOnly BusinessDate { get; set; }
    public long AuditorId { get; set; }
    public long ManagerId { get; set; }
    public string HandoffIdsJson { get; set; } = "[]";
    public string Status { get; set; } = "PendingAcceptance";
    public string? Note { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}
