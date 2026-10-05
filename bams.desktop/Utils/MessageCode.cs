namespace bams.desktop.Utils;

/// <summary>
/// Stable application message identifiers used by the WPF client.
/// Values below 6000 mirror the server's MessageCode enum and must keep the same numbers.
/// Values from 6000 are client-only.
/// </summary>
public enum MessageCode
{
    // Success: 1000 - 1999
    Success = 1000,
    UserCreatedSuccessfully = 1001,
    UserUpdatedSuccessfully = 1002,
    UserPasswordResetSuccessfully = 1003,
    UserDeletedSuccessfully = 1004,
    InterestRateCreatedSuccessfully = 1400,
    InterestRateUpdatedSuccessfully = 1401,
    FeeRuleCreatedSuccessfully = 1402,
    FeeRuleUpdatedSuccessfully = 1403,
    BankPolicyCreatedSuccessfully = 1404,
    BankPolicyUpdatedSuccessfully = 1405,
    CustomerCreatedSuccessfully = 1300,
    TransactionCompletedSuccessfully = 1200,
    TransactionSubmittedSuccessfully = 1201,
    TransactionRefundedSuccessfully = 1202,
    InterbankTransferSettledSuccessfully = 1203,
    NrcPickupCodeReissuedSuccessfully = 1204,

    // Validation: 3000 - 3999
    ValidationFailed = 3000,
    RequiredFieldMissing = 3001,
    InvalidRequest = 3002,
    InvalidAmount = 3003,
    ReconciliationDateRangeTooLong = 3015,
    InvalidReconciliationExceptionStatus = 3016,
    ReconciliationStatusTransitionInvalid = 3017,
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
    InterestRateBalanceRangeInvalid = 3400,
    FeeRuleAmountRangeInvalid = 3401,
    BankPolicyBalanceRangeInvalid = 3402,

    CustomerFullNameRequired = 3200,
    CustomerDateOfBirthInvalid = 3201,
    CustomerBelowMinimumAge = 3202,
    NrcNumberRequired = 3203,
    PassportNumberRequired = 3204,
    CustomerDocumentFileEmpty = 3205,
    CustomerEmailInvalid = 3206,
    SameSourceAndDestinationAccount = 3300,
    InvalidPickupCode = 3301,

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
    AccountTypeNotFound = 4203,

    AccountNotFound = 4201,
    CustomerNotFound = 4202,
    AccountHolderKycNotVerified = 3132,
    UserNotFound = 4204,
    InterestRateRuleNotFound = 4250,
    FeeRuleNotFound = 4251,

    TransactionNotFound = 4240,
    OtherBankNotFound = 4241,
    BranchNotFound = 4242,

    // Conflict: 4300 - 4399
    UsernameAlreadyExists = 4305,
    EmailAlreadyExists = 4306,
    CustomerAlreadyExists = 4307,
    IdempotencyKeyReused = 4330,
    AccountTypeCodeAlreadyExists = 4340,

    // Business Rules: 4400 - 4499
    InsufficientBalance = 4440,
    AccountNotOperational = 4441,
    PickupCodeExpired = 4442,
    TransactionNotPendingPickup = 4443,
    WithdrawalNotAllowed = 4444,
    TransferNotAllowed = 4445,
    MinimumBalanceRequired = 4446,
    DailyTransactionLimitExceeded = 4447,
    MonthlyTransactionLimitExceeded = 4448,
    PickupAttemptsExceeded = 4449,
    TransactionNotPending = 4450,
    NrcPickupLocationMismatch = 4451,
    NrcPickupReceiverMismatch = 4452,
    TransactionMissingAccountingEntries = 4460,
    TransactionEntriesUnbalanced = 4461,
    DailyAccountingUnbalanced = 4462,
    TransactionAccountingEntriesIncomplete = 4465,
    ReconciliationExceptionNotResolvable = 4466,
    CashSessionNotOpen = 4467,
    BusinessDateClosed = 4468,
    BusinessDateTransitionConflict = 4469,
    ReconciliationBlocked = 4470,
    EndOfDayApprovalRequired = 4471,
    CashAdjustmentApprovalRequired = 4472,
    MonthlyAccountingPeriodNotClosed = 4473,
    ScheduledJobRetryUnavailable = 4474,
    CashSessionAlreadyExists = 4475,
    InsufficientCashPositionBalance = 4476,
    CashHandoffRecipientRequired = 4477,
    CashHandoffNotPending = 4478,
    LastManagerCannotBeRemoved = 4480,

    // Server / Infrastructure: 5000 - 5999
    InternalServerError = 5000,

    // Client / WPF: 6000 - 6999
    ClientError = 6000,
    NetworkUnavailable = 6001,
    RequestTimeout = 6002,
    InvalidServerResponse = 6003,
    ScheduledJobAlertsUnavailable = 6020,
    CurrentPasswordIncorrect = 6010
}
