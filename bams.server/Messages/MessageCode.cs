namespace bams.server.Messages;

public enum MessageCode
{
    // Success: 1000 - 1999
    Success = 1000,
    UserCreatedSuccessfully = 1001,
    UserUpdatedSuccessfully = 1002,
    UserPasswordResetSuccessfully = 1003,
    UserDeletedSuccessfully = 1004,
    AccountCreatedSuccessfully = 1100,
    AccountStatusUpdatedSuccessfully = 1101,
    AccountBalanceUpdatedSuccessfully = 1102,
    AccountHoldersUpdatedSuccessfully = 1103,
    FixedDepositUpdatedSuccessfully = 1104,

    // Validation: 3000 - 3999
    ValidationFailed = 3000,
    RequiredFieldMissing = 3001,
    InvalidRequest = 3002,
    InvalidAmount = 3003,
    InvalidCredentials = 3004,
    PasswordDoesNotMeetRequirements = 3005,
    InvalidEmailFormat = 3006,
    InvalidUsernameFormat = 3007,
    InvalidPhoneFormat = 3008,
    InvalidRole = 3009,
    FieldTooLong = 3010,
    InvalidDateRange = 3011,
    NewPasswordSameAsCurrent = 3012,
    DefaultPasswordNotAllowed = 3013,
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
    AccountCursorInvalid = 3127,

    // Authentication: 4000 - 4099
    AuthenticationRequired = 4000,
    PasswordChangeRequired = 4001,
    PasswordChangedSuccessfully = 4002,
    UserAccountDisabled = 4003,
    UserAccountDeleted = 4004,

    // Authorization: 4100 - 4199
    AccessDenied = 4100,
    InsufficientPermission = 4101,

    // Not Found: 4200 - 4299
    ResourceNotFound = 4200,
    AccountNotFound = 4201,
    CustomerNotFound = 4202,
    AccountTypeNotFound = 4203,
    UserNotFound = 4204,
    FixedDepositNotFound = 4205,
    InterestRateRuleNotFound = 4206,
    PayoutAccountNotFound = 4207,
    RequiredPayoutAccountNotFound = 4208,

    // Conflict: 4300 - 4399
    UsernameAlreadyExists = 4305,
    EmailAlreadyExists = 4306,

    // Conflict: 4300 - 4399
    ConcurrentModification = 4304,

    // Business Rules: 4400 - 4499
    BusinessRuleViolation = 4400,
    AccountTypeIdentifierOutOfRange = 4401,
    AccountNumberSequenceExhausted = 4402,
    AccountStatusTransitionNotAllowed = 4403,
    AccountHolderUpdateNotAllowed = 4404,
    InterestRateRuleNotApplicable = 4405,
    RequiredPayoutAccountNotConfigured = 4406,
    FixedDepositStatusTransitionNotAllowed = 4407,
    LastManagerCannotBeRemoved = 4480,

    // System / Infrastructure: 5000 - 5999
    InternalServerError = 5000,
    FileStorageFailed = 5001
}
