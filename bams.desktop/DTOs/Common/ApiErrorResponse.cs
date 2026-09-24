namespace bams.desktop.DTOs.Common;

/// <summary>
/// Standard error body returned by the server for every failed request.
/// </summary>
public sealed record ApiErrorResponse(
    int Code,
    string Name,
    string Message,
    string? TraceId = null);
