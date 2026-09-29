namespace bams.desktop.DTOs.Accounts;

public sealed record AccountPageResponse(
    IReadOnlyList<AccountSummaryResponse> Items,
    bool HasMore,
    string? NextCursor);
