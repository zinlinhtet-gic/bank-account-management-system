using bams.server.Models.Security;

namespace bams.server.Models.Accounting;

/// <summary>Manager-authorized reservation and custody issue from the vault to a teller.</summary>
public sealed class CashProposal
{
    public long Id { get; set; }
    public long SessionId { get; set; }
    public CashPositionSession? Session { get; set; }
    public long VaultSessionId { get; set; }
    public CashPositionSession? VaultSession { get; set; }
    public long OfficerId { get; set; }
    public User? Officer { get; set; }
    public decimal? RequestedAmount { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public bool IsInitialCash { get; set; }
    public string Status { get; set; } = "PendingApproval";
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public long? ApprovedBy { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public long Version { get; set; } = 1;
}
