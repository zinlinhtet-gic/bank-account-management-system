namespace bams.desktop.Utils;

/// <summary>
/// Resolves user-facing text from stable application message codes.
/// Server errors normally carry their own message; these entries are the client-side fallback.
/// </summary>
public static class MessageCatalog
{
    private static readonly IReadOnlyDictionary<MessageCode, string> Messages =
        new Dictionary<MessageCode, string>
        {
            [MessageCode.Success] = "Operation completed successfully.",
            [MessageCode.ValidationFailed] = "One or more validation errors occurred.",
            [MessageCode.RequiredFieldMissing] = "Please fill in all required fields.",
            [MessageCode.InvalidRequest] = "The request is invalid.",
            [MessageCode.InvalidCredentials] = "Invalid username or password.",
            [MessageCode.PasswordDoesNotMeetRequirements] = "The password does not meet the requirements.",
            [MessageCode.AuthenticationRequired] = "Your session has expired. Please log in again.",
            [MessageCode.PasswordChangeRequired] = "You must change your password before continuing.",
            [MessageCode.PasswordChangedSuccessfully] = "Password changed successfully.",
            [MessageCode.UserAccountDisabled] = "This user account is disabled. Please contact your administrator.",
            [MessageCode.AccessDenied] = "Access denied.",
            [MessageCode.InsufficientPermission] = "You do not have permission to perform this operation.",
            [MessageCode.ResourceNotFound] = "The requested item was not found.",
            [MessageCode.UserNotFound] = "User was not found.",
            [MessageCode.UsernameAlreadyExists] = "A user with this username already exists.",
            [MessageCode.EmailAlreadyExists] = "A user with this email already exists.",
            [MessageCode.InternalServerError] = "An unexpected server error occurred.",
            [MessageCode.ClientError] = "The application could not complete the request.",
            [MessageCode.NetworkUnavailable] = "The server cannot currently be reached. Please check your connection and try again.",
            [MessageCode.RequestTimeout] = "The request timed out. Please try again.",
            [MessageCode.InvalidServerResponse] = "The server returned an invalid response.",
            [MessageCode.CurrentPasswordIncorrect] = "Current password is incorrect. Please verify it and try again."
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
