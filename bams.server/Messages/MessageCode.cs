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
    OpeningBalanceInvalid = 3102,
    InvalidDate = 3004,

    // Authorization: 4100 - 4199
    AccessDenied = 4100,

    // Not Found: 4200 - 4299
    AccountNotFound = 4201,
    AccountTypeNotFound = 4203,
    ResourceNotFound = 4204,

    // Business Rules: 4400 - 4499
    BusinessRuleViolation = 4400,

    // System / Infrastructure: 5000 - 5999
    InternalServerError = 5000
}
