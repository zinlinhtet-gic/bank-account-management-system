namespace bams.desktop.DTOs.Common;

/// <summary>
/// One page of a server list, with the total number of matching items (server: <c>DTO/Common/PagedResponse</c>).
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
