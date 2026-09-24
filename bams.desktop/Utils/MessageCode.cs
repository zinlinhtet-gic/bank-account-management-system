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

    // Validation: 3000 - 3999
    ValidationFailed = 3000,
    InvalidRequest = 3002,
    InvalidCredentials = 3004,
    PasswordDoesNotMeetRequirements = 3005,

    // Authentication: 4000 - 4099
    AuthenticationRequired = 4000,
    PasswordChangeRequired = 4001,
    PasswordChangedSuccessfully = 4002,
    UserAccountDisabled = 4003,

    // Authorization: 4100 - 4199
    AccessDenied = 4100,
    InsufficientPermission = 4101,

    // Not Found: 4200 - 4299
    ResourceNotFound = 4200,
    UserNotFound = 4204,

    // Conflict: 4300 - 4399
    UsernameAlreadyExists = 4305,
    EmailAlreadyExists = 4306,

    // Server / Infrastructure: 5000 - 5999
    InternalServerError = 5000,

    // Client / WPF: 6000 - 6999
    ClientError = 6000,
    NetworkUnavailable = 6001,
    RequestTimeout = 6002,
    InvalidServerResponse = 6003,
    CurrentPasswordIncorrect = 6010
}
