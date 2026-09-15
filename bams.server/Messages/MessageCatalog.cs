namespace bams.server.Messages;

public static class MessageCatalog
{
    private static readonly IReadOnlyDictionary<MessageCode, string> Messages =
        new Dictionary<MessageCode, string>
        {
            [MessageCode.Success] = "Operation completed successfully.",
            [MessageCode.AccountCreatedSuccessfully] = "Account created successfully.",
            [MessageCode.ValidationFailed] = "One or more validation errors occurred.",
            [MessageCode.InvalidRequest] = "The request is invalid.",
            [MessageCode.InvalidAmount] = "The provided amount is invalid.",
            [MessageCode.AccountNameRequired] = "Account name is required.",
            [MessageCode.AccountTypeInvalid] = "Account type is invalid.",
            [MessageCode.OpeningBalanceInvalid] = "Opening balance is invalid.",
            [MessageCode.AccountNotFound] = "Account was not found.",
            [MessageCode.AccountAlreadyExists] = "Account already exists.",
            [MessageCode.InternalServerError] = "An unexpected error occurred."
        };

    // Resolves the human-readable message associated with a stable message code.
    public static string GetMessage(MessageCode code)
    {
        return Messages.TryGetValue(code, out var message)
            ? message
            : "Unknown application message.";
    }
}
