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
            [MessageCode.InvalidCredentials] = "Invalid username or password.",
            [MessageCode.OpeningBalanceInvalid] = "Opening balance is below the account type's minimum.",
            [MessageCode.AuthenticationRequired] = "Authentication is required.",
            [MessageCode.PasswordChangeRequired] = "You must change your password before continuing.",
            [MessageCode.PasswordChangedSuccessfully] = "Password changed successfully.",
            [MessageCode.AccessDenied] = "Access denied.",
            [MessageCode.AccountNotFound] = "Account was not found.",
            [MessageCode.AccountTypeNotFound] = "Account type was not found.",
            [MessageCode.BusinessRuleViolation] = "The requested operation violates a business rule.",
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
