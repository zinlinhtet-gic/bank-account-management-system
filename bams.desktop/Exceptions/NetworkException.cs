namespace bams.desktop.Exceptions;

/// <summary>
/// Exception thrown when network connectivity issues occur.
/// </summary>
public sealed class NetworkException : Exception
{
    public NetworkException(string message) : base(message)
    {
    }

    public NetworkException(string message, Exception innerException) : base(message, innerException)
    {
    }
}