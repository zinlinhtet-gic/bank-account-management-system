using bams.server.Messages;

namespace bams.server.Exceptions;

public sealed class ValidationException : AppException
{
    // Creates a validation exception associated with a stable application message code.
    public ValidationException(MessageCode code)
        : base(code)
    {
    }
}
