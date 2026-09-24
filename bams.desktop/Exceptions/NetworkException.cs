using bams.desktop.Utils;

namespace bams.desktop.Exceptions;

/// <summary>
/// Raised when the server cannot be reached or does not answer in time.
/// </summary>
public sealed class NetworkException : AppException
{
    // Wraps a transport failure so ViewModels never display raw framework exception text.
    public NetworkException(
        MessageCode code,
        Exception innerException)
        : base(code, innerException)
    {
    }
}
