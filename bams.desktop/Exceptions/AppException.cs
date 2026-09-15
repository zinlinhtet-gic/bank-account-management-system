using bams.desktop.Utils;

namespace bams.desktop.Exceptions;

/// <summary>
/// Base type for expected application exceptions that carry a stable message code.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(
        MessageCode code)
        : base(MessageCatalog.GetMessage(code))
    {
        Code = code;
    }

    protected AppException(
        MessageCode code,
        string message)
        : base(message)
    {
        Code = code;
    }

    public MessageCode Code { get; }
}
