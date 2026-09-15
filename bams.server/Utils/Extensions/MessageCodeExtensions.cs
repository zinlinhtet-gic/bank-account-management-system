using bams.server.Messages;

namespace bams.server.Utils.Extensions;

public static class MessageCodeExtensions
{
    /// <summary>
    /// Converts a message code into the standard API response name.
    /// </summary>
    public static string ToResponseName(this MessageCode code)
    {
        return code.ToString();
    }
}
