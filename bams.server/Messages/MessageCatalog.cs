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
            [MessageCode.PasswordDoesNotMeetRequirements] = "Password must be at least 8 characters long and include: 1 uppercase letter (A-Z), 1 lowercase letter (a-z), 1 number (0-9), and 1 special character (!@#$%^&*).",
            [MessageCode.OpeningBalanceInvalid] = "Opening balance is below the account type's minimum.",
            [MessageCode.AuthenticationRequired] = "Authentication is required.",
            [MessageCode.PasswordChangeRequired] = "You must change your password before continuing.",
            [MessageCode.PasswordChangedSuccessfully] = "Password changed successfully.",
            [MessageCode.HolderAlreadyHasActiveAccount] = "The customer already has an active individual account of this type.",
            [MessageCode.SharedAccountRequiresTwoHolders] = "A shared account requires two account holders.",
            [MessageCode.SharedAccountRequiresTwoOwnershipPercentages] = "A shared account requires an ownership percentage for each holder.",
            [MessageCode.OwnershipPercentageOutOfRange] = "Each ownership percentage must be greater than zero and no greater than 100.",
            [MessageCode.SharedAccountOwnershipPercentagesMustSumTo100] = "Shared account ownership percentages must total 100.",
            [MessageCode.RequiredAccountDocumentMissing] = "One or more required account documents are missing.",
            [MessageCode.DuplicateAccountDocumentType] = "Only one account document may be uploaded for each document type.",
            [MessageCode.UploadedFileEmpty] = "An uploaded document file is empty.",
            [MessageCode.UploadedFileTooLarge] = "An uploaded document exceeds the maximum permitted size.",
            [MessageCode.UnsupportedDocumentFileType] = "The uploaded document file type is not supported.",
            [MessageCode.InvalidDocumentFileContent] = "The uploaded document content does not match its file type.",
            [MessageCode.InvalidDocumentFileReference] = "The document file reference is invalid.",
            [MessageCode.UnsupportedAccountDocumentType] = "The account document type is not supported.",
            [MessageCode.UploadedFileNameTooLong] = "The uploaded document filename is too long.",
            [MessageCode.AccountDocumentNumberTooLong] = "The account document number is too long.",
            [MessageCode.AccessDenied] = "Access denied.",
            [MessageCode.AccountNotFound] = "Account was not found.",
            [MessageCode.CustomerNotFound] = "Customer was not found for the provided NRC.",
            [MessageCode.AccountTypeNotFound] = "Account type was not found.",
            [MessageCode.BusinessRuleViolation] = "The requested operation violates a business rule.",
            [MessageCode.AccountTypeIdentifierOutOfRange] = "Account type identifier must fit the two-digit account number segment.",
            [MessageCode.AccountNumberSequenceExhausted] = "The hourly account number sequence is exhausted for this account type.",
            [MessageCode.InternalServerError] = "An unexpected error occurred.",
            [MessageCode.FileStorageFailed] = "The uploaded document could not be stored."
        };

    // Resolves the human-readable message associated with a stable message code.
    public static string GetMessage(MessageCode code)
    {
        return Messages.TryGetValue(code, out var message)
            ? message
            : "Unknown application message.";
    }
}
