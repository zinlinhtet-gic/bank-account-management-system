namespace bams.server.DTO.Accounting;

public sealed record BusinessDateResponse(DateOnly Date, string Status, DateTime OpenedAtUtc, DateTime? ClosedAtUtc);
public sealed record EndOfDayStageResponse(string Name, string Status, int IssueCount, string? MessageCode);
public sealed record EndOfDayRunResponse(long RunId, DateOnly BusinessDate, string Status, DateTime PreparedAtUtc,
    IReadOnlyList<EndOfDayStageResponse> Stages, long PreparedBy, long? ApprovedBy, long? ClosedBy)
{
    public long? ReviewedBy { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string? OverrideReason { get; init; }
    public IReadOnlyList<long> OverrideRequiredUserIds { get; init; } = [];
    public IReadOnlyList<long> OverrideApprovedUserIds { get; init; } = [];
    public IReadOnlyDictionary<long, DateTime> OverrideApprovalTimesUtc { get; init; } = new Dictionary<long, DateTime>();
}
public sealed record ForceCloseRequest(string Reason);
