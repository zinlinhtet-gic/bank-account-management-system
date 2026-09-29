namespace bams.desktop.DTOs.Accounts;

public sealed record AccountStatusHistoryResponse(
    long Id,
    string OldStatus,
    string NewStatus,
    string? Reason,
    string ChangedBy,
    DateTime ChangedAt);
