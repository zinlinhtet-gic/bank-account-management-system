namespace bams.server.Messages;

public enum MessageCode
{
    // Success: 1000 - 1999
    Success = 1000,
    AccountCreatedSuccessfully = 1100,

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

    // Business Rules: 4400 - 4499
    BusinessRuleViolation = 4400,
    AccountTypeIdentifierOutOfRange = 4401,
    AccountNumberSequenceExhausted = 4402,

    // System / Infrastructure: 5000 - 5999
    InternalServerError = 5000,
    FileStorageFailed = 5001
}
