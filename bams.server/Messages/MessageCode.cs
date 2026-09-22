namespace bams.server.Messages;

public enum MessageCode
{
    // Success: 1000 - 1999
    Success = 1000,
    AccountCreatedSuccessfully = 1100,
    CustomerCreatedSuccessfully = 1300,

    // Validation: 3000 - 3999
    ValidationFailed = 3000,
    InvalidRequest = 3002,
    InvalidAmount = 3003,
    OpeningBalanceInvalid = 3102,
    CustomerFullNameRequired = 3200,
    CustomerDateOfBirthInvalid = 3201,
    CustomerBelowMinimumAge = 3202,
    NrcNumberRequired = 3203,
    PassportNumberRequired = 3204,
    CustomerDocumentFileEmpty = 3205,

    CustomerEmailInvalid = 3206,

    // Authorization: 4100 - 4199
    AccessDenied = 4100,

    // Not Found: 4200 - 4299
    AccountNotFound = 4201,
    CustomerNotFound = 4202,
    AccountTypeNotFound = 4203,

    // Conflict: 4300 - 4399
    CustomerAlreadyExists = 4305,

    // Business Rules: 4400 - 4499
    BusinessRuleViolation = 4400,

    // System / Infrastructure: 5000 - 5999
    InternalServerError = 5000
}
