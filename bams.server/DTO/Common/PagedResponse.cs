namespace bams.server.DTO.Common;

/// <summary>
/// One page of a list, with the total number of matching items so clients can show page controls.
/// </summary>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
