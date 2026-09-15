namespace bams.desktop.Utils;

/// <summary>
/// Resolves user-facing text from stable application message codes.
/// </summary>
public static class MessageCatalog
{
    private static readonly IReadOnlyDictionary<MessageCode, string> Messages =
        new Dictionary<MessageCode, string>
        {
            [MessageCode.Success] = "Operation completed successfully.",
            [MessageCode.ClientError] = "The application could not complete the request.",
            [MessageCode.NetworkUnavailable] = "The server cannot currently be reached.",
            [MessageCode.InvalidServerResponse] = "The server returned an invalid response."
        };

    // Resolves the display message for a stable message code.
    public static string GetMessage(
        MessageCode code)
    {
        return Messages.TryGetValue(
            code,
            out var message)
            ? message
            : "Unknown application message.";
    }
}
