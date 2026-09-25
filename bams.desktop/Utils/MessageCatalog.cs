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
            [MessageCode.UserCreatedSuccessfully] = "User created successfully.",
            [MessageCode.UserUpdatedSuccessfully] = "User updated successfully.",
            [MessageCode.UserPasswordResetSuccessfully] = "Password reset successfully. The user must change it at the next login.",
            [MessageCode.UserDeletedSuccessfully] = "User deleted successfully.",
            [MessageCode.TransactionCompletedSuccessfully] = "Transaction completed successfully.",
            [MessageCode.TransactionSubmittedSuccessfully] = "Transfer submitted and is pending completion.",
            [MessageCode.TransactionRefundedSuccessfully] = "The transfer was stopped and the amount refunded to the sender's account.",
            [MessageCode.InterbankTransferSettledSuccessfully] = "The interbank transfer was marked as settled.",
            [MessageCode.InvalidAmount] = "Enter an amount greater than zero with at most 2 decimal places.",
            [MessageCode.SameSourceAndDestinationAccount] = "The source and destination accounts must be different.",
            [MessageCode.InvalidPickupCode] = "The pickup code is incorrect.",
            [MessageCode.AccountNotFound] = "Account was not found.",
            [MessageCode.TransactionNotFound] = "Transaction was not found.",
            [MessageCode.OtherBankNotFound] = "The destination bank was not found.",
            [MessageCode.BranchNotFound] = "The pickup branch was not found.",
            [MessageCode.IdempotencyKeyReused] = "This request was already submitted with different details. Close the form and start again.",
            [MessageCode.InsufficientBalance] = "The account has insufficient available balance.",
            [MessageCode.AccountNotOperational] = "The account is closed, frozen or suspended and cannot be used for transactions.",
            [MessageCode.PickupCodeExpired] = "The pickup code has expired. Cancel the transfer to refund the sender.",
            [MessageCode.TransactionNotPendingPickup] = "This transfer has already been picked up or is no longer pending.",
            [MessageCode.WithdrawalNotAllowed] = "Withdrawals are not allowed for this account type.",
            [MessageCode.TransferNotAllowed] = "Transfers are not allowed for this account type.",
            [MessageCode.MinimumBalanceRequired] = "This would take the account below its minimum balance.",
            [MessageCode.DailyTransactionLimitExceeded] = "This exceeds the account's daily limit.",
            [MessageCode.MonthlyTransactionLimitExceeded] = "This exceeds the account's monthly limit.",
            [MessageCode.PickupAttemptsExceeded] = "Too many wrong pickup codes. The transfer is blocked and can only be cancelled.",
            [MessageCode.TransactionNotPending] = "This transaction is no longer pending.",
            [MessageCode.NrcPickupLocationMismatch] = "This transfer is paid out elsewhere. Our branches enter the pickup code; for another bank, record its payout.",
            [MessageCode.ValidationFailed] ="One or more validation errors occurred.",
            [MessageCode.RequiredFieldMissing] = "Please fill in all required fields.",
            [MessageCode.InvalidRequest] = "The request is invalid.",
            [MessageCode.InvalidCredentials] = "Invalid username or password.",
            [MessageCode.PasswordDoesNotMeetRequirements] = "The password does not meet the requirements.",
            [MessageCode.InvalidEmailFormat] = "Please enter a valid email address.",
            [MessageCode.InvalidUsernameFormat] = "3-64 characters: letters, numbers, dot, underscore or hyphen. No spaces.",
            [MessageCode.InvalidPhoneFormat] = "Please enter a valid phone number.",
            [MessageCode.InvalidRole] = "Please choose a role.",
            [MessageCode.FieldTooLong] = "This value is too long.",
            [MessageCode.InvalidDateRange] = "The start date must be on or before the end date.",
            [MessageCode.NewPasswordSameAsCurrent] = "The new password must be different from your current password.",
            [MessageCode.DefaultPasswordNotAllowed] = "You cannot use a default password. Please choose your own password.",
            [MessageCode.UserAccountDeleted] = "This user account has been deleted. Please contact your administrator.",
            [MessageCode.LastManagerCannotBeRemoved] = "This is the only active manager. Add another manager first.",
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
