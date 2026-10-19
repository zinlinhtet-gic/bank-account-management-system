namespace bams.desktop.DTOs.Accounting;

public sealed record BusinessDateResponse(DateOnly Date, string Status, DateTime OpenedAtUtc, DateTime? ClosedAtUtc, bool IsActive = false, bool CanStartNewDate = false, DateOnly? Today = null)
{
    public CashPositionSessionResponse? Vault { get; init; }
    public IReadOnlyList<CashPositionSessionResponse> Sessions { get; init; } = [];
}
public sealed record StartBusinessDateRequest(decimal? StartingCash, IReadOnlyList<ScheduledCashSessionRequest>? Sessions = null, string? IdempotencyKey = null, DateOnly? ExpectedDate = null);
public sealed record EndOfDayStageResponse(string Name, string Status, int IssueCount, string? MessageCode);
public sealed record EndOfDayRunResponse(long RunId, DateOnly BusinessDate, string Status, DateTime PreparedAtUtc,
    IReadOnlyList<EndOfDayStageResponse> Stages, long PreparedBy, long? ApprovedBy, long? ClosedBy)
{
    public EndOfDayRunResponse? PendingForceClose { get; init; }
    public long? ReviewedBy { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string? OverrideReason { get; init; }
    public IReadOnlyList<long> OverrideRequiredUserIds { get; init; } = [];
    public IReadOnlyList<long> OverrideApprovedUserIds { get; init; } = [];
    public IReadOnlyDictionary<long, DateTime> OverrideApprovalTimesUtc { get; init; } = new Dictionary<long, DateTime>();
}
public sealed record ForceCloseRequest(string Reason);
