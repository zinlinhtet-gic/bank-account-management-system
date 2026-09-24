namespace bams.desktop.DTOs.Common;

/// <summary>
/// Standard success envelope returned by the server, wrapping the payload in <c>Data</c>.
/// </summary>
public sealed record ApiMessageResponse<T>(
    int Code,
    string Name,
    string Message,
    T? Data);
