namespace bams.server.DTO.Accounting;

public sealed record CreateAuditorCashReturnRequest(long ManagerId, IReadOnlyList<long> HandoffIds, string? Note);
public sealed record AuditorCashReturnResponse(long Id, DateOnly BusinessDate, long AuditorId, long ManagerId,
    IReadOnlyList<long> HandoffIds, string Status, string? Note, string? DecisionNote,
    DateTime CreatedAtUtc, DateTime? DecidedAtUtc, long Version);
