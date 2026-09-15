namespace bams.server.DTO.Common;

public sealed record ApiErrorResponse(
    int Code,
    string Name,
    string Message,
    string? TraceId = null);
