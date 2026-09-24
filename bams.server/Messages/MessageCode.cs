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
    CustomerCreatedSuccessfully = 1300,
    CustomerUpdatedSuccessfully = 1301,
    CustomerKycReviewedSuccessfully = 1302,

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
    CustomerFullNameRequired = 3200,
    CustomerDateOfBirthInvalid = 3201,
    CustomerBelowMinimumAge = 3202,
    NrcNumberRequired = 3203,
    PassportNumberRequired = 3204,
    CustomerDocumentFileEmpty = 3205,

    CustomerEmailInvalid = 3206,
    CustomerDocumentTypeRequired = 3207,
    InvalidKycReviewStatus = 3208,

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
    CustomerDocumentNotFound = 4204,
    KycReviewerNotFound = 4205,

    // Conflict: 4300 - 4399
    CustomerAlreadyExists = 4305,
    UserNotFound = 4204,

    // Conflict: 4300 - 4399
    UsernameAlreadyExists = 4305,
    EmailAlreadyExists = 4306,

    // Business Rules: 4400 - 4499
    BusinessRuleViolation = 4400,
    LastManagerCannotBeRemoved = 4480,

    // System / Infrastructure: 5000 - 5999
    InternalServerError = 5000
}
