using bams.server.Messages;

namespace bams.server.Exceptions;

public sealed class BusinessRuleException : AppException
{
    // Creates a business-rule exception associated with a stable application message code.
    public BusinessRuleException(MessageCode code)
        : base(code)
    {
    }
}
