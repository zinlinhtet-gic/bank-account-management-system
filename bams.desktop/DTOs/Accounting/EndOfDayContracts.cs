namespace bams.desktop.DTOs.Accounting;

public sealed record BusinessDateResponse(DateOnly Date, string Status, DateTime OpenedAtUtc, DateTime? ClosedAtUtc);
public sealed record EndOfDayStageResponse(string Name, string Status, int IssueCount, string? MessageCode);
public sealed record EndOfDayRunResponse(long RunId, DateOnly BusinessDate, string Status, DateTime PreparedAtUtc,
    IReadOnlyList<EndOfDayStageResponse> Stages, long PreparedBy, long? ApprovedBy, long? ClosedBy);
