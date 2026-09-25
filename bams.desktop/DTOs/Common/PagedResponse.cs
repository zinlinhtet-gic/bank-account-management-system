namespace bams.desktop.DTOs.Common;

/// <summary>
/// Mirrors the server's paged list envelope (e.g. GET /api/customers).
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
