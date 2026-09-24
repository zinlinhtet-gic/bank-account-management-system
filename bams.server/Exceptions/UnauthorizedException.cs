using bams.server.Messages;

namespace bams.server.Exceptions;

public sealed class UnauthorizedException : AppException
{
    // Creates an authentication exception associated with a stable application message code.
    public UnauthorizedException(MessageCode code)
        : base(code)
    {
    }
}
