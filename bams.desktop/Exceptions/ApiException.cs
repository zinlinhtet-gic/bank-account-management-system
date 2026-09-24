using bams.desktop.Utils;

namespace bams.desktop.Exceptions;

/// <summary>
/// Raised when the server rejects a request or returns a response the client cannot read.
/// </summary>
public sealed class ApiException : AppException
{
    // Creates an API exception whose text comes from the client message catalog.
    public ApiException(
        MessageCode code)
        : base(code)
    {
    }

    // Creates an API exception from a server ApiErrorResponse, keeping the server's message and trace id.
    public ApiException(
        MessageCode code,
        string message,
        string? traceId)
        : base(code, message)
    {
        TraceId = traceId;
    }

    /// <summary>
    /// The server trace identifier, useful when reporting a problem to support.
    /// </summary>
    public string? TraceId { get; }
}
