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
    InvalidDate = 3004,

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
    AccountTypeNotFound = 4203,
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
