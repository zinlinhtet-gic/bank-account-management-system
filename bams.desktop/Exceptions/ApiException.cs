namespace bams.desktop.Exceptions;

/// <summary>
/// Exception thrown when API-related errors occur.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(string message) : base(message)
    {
    }

    public ApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}