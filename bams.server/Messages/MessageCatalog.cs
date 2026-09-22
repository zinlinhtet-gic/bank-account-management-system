namespace bams.server.Messages;

public static class MessageCatalog
{
    private static readonly IReadOnlyDictionary<MessageCode, string> Messages =
        new Dictionary<MessageCode, string>
        {
            [MessageCode.Success] = "Operation completed successfully.",
            [MessageCode.AccountCreatedSuccessfully] = "Account created successfully.",
            [MessageCode.AccountStatusUpdatedSuccessfully] = "Account status updated successfully.",
            [MessageCode.AccountBalanceUpdatedSuccessfully] = "Account balance updated successfully.",
            [MessageCode.AccountHoldersUpdatedSuccessfully] = "Account holders updated successfully.",
            [MessageCode.FixedDepositUpdatedSuccessfully] = "Fixed deposit updated successfully.",
            [MessageCode.ValidationFailed] = "One or more validation errors occurred.",
            [MessageCode.InvalidRequest] = "The request is invalid.",
            [MessageCode.InvalidAmount] = "The provided amount is invalid.",
            [MessageCode.OpeningBalanceInvalid] = "Opening balance is below the account type's minimum.",
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
            [MessageCode.AccountStatusInvalid] = "The requested account status is invalid.",
            [MessageCode.AccountBalanceCannotBeNegative] = "The account balance cannot be negative.",
            [MessageCode.CustomerDoesNotHaveRequiredProducts] = "The customer does not have the product required for this account type.",
            [MessageCode.AccountHolderSelectionInvalid] = "The account holder selection must contain both existing holders and exactly one primary holder.",
            [MessageCode.AccountHolderSigningRuleTooLong] = "The account holder signing rule exceeds the maximum permitted length.",
            [MessageCode.SharedAccountRequiresExactlyOnePrimaryHolder] = "A shared account must have exactly one primary holder.",
            [MessageCode.FixedDepositRequestInvalid] = "The fixed-deposit request is incomplete or invalid.",
            [MessageCode.FixedDepositCurrentPrincipalInvalid] = "The fixed-deposit current principal cannot be negative.",
            [MessageCode.FixedDepositUpdateRequiresChanges] = "At least one fixed-deposit field must be supplied for update.",
            [MessageCode.AccessDenied] = "Access denied.",
            [MessageCode.AccountNotFound] = "Account was not found.",
            [MessageCode.CustomerNotFound] = "Customer was not found for the provided NRC.",
            [MessageCode.AccountTypeNotFound] = "Account type was not found.",
            [MessageCode.FixedDepositNotFound] = "Fixed deposit was not found.",
            [MessageCode.InterestRateRuleNotFound] = "Interest rate rule was not found.",
            [MessageCode.PayoutAccountNotFound] = "Payout account was not found.",
            [MessageCode.RequiredPayoutAccountNotFound] = "The primary holder does not have the required payout account.",
            [MessageCode.BusinessRuleViolation] = "The requested operation violates a business rule.",
            [MessageCode.AccountTypeIdentifierOutOfRange] = "Account type identifier must fit the two-digit account number segment.",
            [MessageCode.AccountNumberSequenceExhausted] = "The hourly account number sequence is exhausted for this account type.",
            [MessageCode.AccountStatusTransitionNotAllowed] = "The requested account status transition is not allowed.",
            [MessageCode.AccountHolderUpdateNotAllowed] = "Holder details can be updated only for a non-closed joint account.",
            [MessageCode.InterestRateRuleNotApplicable] = "The interest rate rule is not applicable to this fixed deposit.",
            [MessageCode.RequiredPayoutAccountNotConfigured] = "The fixed-deposit account type does not configure a required payout product.",
            [MessageCode.FixedDepositStatusTransitionNotAllowed] = "The requested fixed-deposit status transition is not allowed.",
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
