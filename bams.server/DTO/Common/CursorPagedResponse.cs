namespace bams.server.DTO.Common;

/// <summary>
/// Represents one forward-only page and the cursor needed to continue the query.
/// </summary>
public sealed record CursorPagedResponse<T>(
    IReadOnlyList<T> Items,
    bool HasMore,
    string? NextCursor);
