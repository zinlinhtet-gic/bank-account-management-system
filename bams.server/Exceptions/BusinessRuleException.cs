using bams.server.Messages;

namespace bams.server.Exceptions;

public sealed class BusinessRuleException : AppException
{
    // Preserves the stable error code while identifying the records that block an operation.
    public BusinessRuleException(MessageCode code, string message) : base(code, message)
    {
    }
    // Creates a business-rule exception associated with a stable application message code.
    public BusinessRuleException(MessageCode code)
        : base(code)
    {
    }
}
