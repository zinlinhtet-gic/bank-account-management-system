using bams.server.Messages;

namespace bams.server.Exceptions;

public sealed class ForbiddenException : AppException
{
    // Creates an authorization exception associated with a stable application message code.
    public ForbiddenException(MessageCode code)
        : base(code)
    {
    }
}
