using bams.server.Messages;

namespace bams.server.Exceptions;

public abstract class AppException : Exception
{
    public MessageCode Code { get; }

    // Creates an application exception using the standard message for the code.
    protected AppException(MessageCode code)
        : base(MessageCatalog.GetMessage(code))
    {
        Code = code;
    }

    // Creates an application exception using contextual text while preserving the stable code.
    protected AppException(
        MessageCode code,
        string message)
        : base(message)
    {
        Code = code;
    }
}
