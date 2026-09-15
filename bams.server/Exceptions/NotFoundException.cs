using bams.server.Messages;

namespace bams.server.Exceptions;

public sealed class NotFoundException : AppException
{
    // Creates a not-found exception associated with a stable application message code.
    public NotFoundException(MessageCode code)
        : base(code)
    {
    }
}
