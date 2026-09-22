namespace bams.server.Messages;

public enum MessageCode
{
    // Success: 1000 - 1999
    Success = 1000,
    AccountCreatedSuccessfully = 1100,
    AccountStatusUpdatedSuccessfully = 1101,
    AccountBalanceUpdatedSuccessfully = 1102,
    AccountHoldersUpdatedSuccessfully = 1103,
    FixedDepositUpdatedSuccessfully = 1104,

    // Validation: 3000 - 3999
    ValidationFailed = 3000,
    InvalidRequest = 3002,
    InvalidAmount = 3003,
    InvalidCredentials = 3004,
    PasswordDoesNotMeetRequirements = 3005,
    OpeningBalanceInvalid = 3102,
    HolderAlreadyHasActiveAccount = 3103,
    SharedAccountRequiresTwoHolders = 3104,
    SharedAccountRequiresTwoOwnershipPercentages = 3105,
    OwnershipPercentageOutOfRange = 3106,
    SharedAccountOwnershipPercentagesMustSumTo100 = 3107,
    RequiredAccountDocumentMissing = 3108,
    DuplicateAccountDocumentType = 3109,
    UploadedFileEmpty = 3110,
    UploadedFileTooLarge = 3111,
    UnsupportedDocumentFileType = 3112,
    InvalidDocumentFileContent = 3113,
    InvalidDocumentFileReference = 3114,
    UnsupportedAccountDocumentType = 3115,
    UploadedFileNameTooLong = 3116,
    AccountDocumentNumberTooLong = 3117,
    AccountStatusInvalid = 3118,
    AccountBalanceCannotBeNegative = 3119,
    CustomerDoesNotHaveRequiredProducts = 3120,
    AccountHolderSelectionInvalid = 3121,
    AccountHolderSigningRuleTooLong = 3122,
    SharedAccountRequiresExactlyOnePrimaryHolder = 3123,
    FixedDepositRequestInvalid = 3124,
    FixedDepositCurrentPrincipalInvalid = 3125,
    FixedDepositUpdateRequiresChanges = 3126,

    // Authentication: 4000 - 4099
    AuthenticationRequired = 4000,
    PasswordChangeRequired = 4001,
    PasswordChangedSuccessfully = 4002,

    // Authorization: 4100 - 4199
    AccessDenied = 4100,
    InsufficientPermission = 4101,

    // Not Found: 4200 - 4299
    ResourceNotFound = 4200,
    AccountNotFound = 4201,
    CustomerNotFound = 4202,
    AccountTypeNotFound = 4203,
    FixedDepositNotFound = 4204,
    InterestRateRuleNotFound = 4205,
    PayoutAccountNotFound = 4206,
    RequiredPayoutAccountNotFound = 4207,

    // Business Rules: 4400 - 4499
    BusinessRuleViolation = 4400,
    AccountTypeIdentifierOutOfRange = 4401,
    AccountNumberSequenceExhausted = 4402,
    AccountStatusTransitionNotAllowed = 4403,
    AccountHolderUpdateNotAllowed = 4404,
    InterestRateRuleNotApplicable = 4405,
    RequiredPayoutAccountNotConfigured = 4406,
    FixedDepositStatusTransitionNotAllowed = 4407,

    // System / Infrastructure: 5000 - 5999
    InternalServerError = 5000,
    FileStorageFailed = 5001
}
