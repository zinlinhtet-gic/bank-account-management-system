using bams.server.Messages;

namespace bams.server.Exceptions;

public sealed class ConflictException : AppException
{
    // Creates a conflict exception associated with a stable application message code.
    public ConflictException(MessageCode code)
        : base(code)
    {
    }
}
