using bams.server.Messages;

namespace bams.server.Exceptions;

public sealed class AuthenticationRequiredException : AppException
{
    // Creates an authentication exception associated with a stable application message code.
    public AuthenticationRequiredException(MessageCode code)
        : base(code)
    {
    }
}
