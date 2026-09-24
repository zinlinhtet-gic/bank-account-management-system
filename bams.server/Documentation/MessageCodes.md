# Message Codes

Codes are stable once released. The WPF client mirrors the server codes below 6000 in
`bams.desktop/Utils/MessageCode.cs` with the same numbers; client-only codes use 6000+.

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 1000 | Success | 200 | General successful operation. |
| 1100 | AccountCreatedSuccessfully | 201 | Account creation succeeded. |
| 3000 | ValidationFailed | 400 | One or more validation errors occurred. |
| 3002 | InvalidRequest | 400 | The request shape or content is invalid. |
| 3003 | InvalidAmount | 400 | A supplied amount is invalid. |
| 3004 | InvalidCredentials | 400 | Username or password is wrong (login), or the current password is wrong (change password). |
| 3005 | PasswordDoesNotMeetRequirements | 400 | New password fails the complexity rules. |
| 3102 | OpeningBalanceInvalid | 400 | Opening balance is below the account type's minimum. |
| 4000 | AuthenticationRequired | 401 | No valid JWT, or the token's user no longer exists. |
| 4001 | PasswordChangeRequired | — | The user must change their password before continuing. |
| 4002 | PasswordChangedSuccessfully | 200 | Password change succeeded. |
| 4003 | UserAccountDisabled | 403 | The user's `Status` is not `Active`; login and protected endpoints are refused. |
| 4100 | AccessDenied | 403 | The user is not allowed to perform the operation. |
| 4101 | InsufficientPermission | 403 | None of the user's roles grants a permission required by `[RequirePermission]`. |
| 4200 | ResourceNotFound | 404 | A generic resource was not found. |
| 4201 | AccountNotFound | 404 | The requested bank account does not exist. |
| 4203 | AccountTypeNotFound | 404 | The requested account type does not exist. |
| 4204 | UserNotFound | 404 | The requested staff user does not exist. |
| 4305 | UsernameAlreadyExists | 409 | Another user already has this username. |
| 4306 | EmailAlreadyExists | 409 | Another user already has this email. |
| 4400 | BusinessRuleViolation | 422 | A business rule was violated. |
| 5000 | InternalServerError | 500 | An unexpected server error occurred. |

## Client-only codes (WPF)

| Code | Name | Meaning |
| --- | --- | --- |
| 6000 | ClientError | Unexpected client-side failure. |
| 6001 | NetworkUnavailable | The server could not be reached. |
| 6002 | RequestTimeout | The server did not answer in time. |
| 6003 | InvalidServerResponse | The server response could not be read. |
| 6010 | CurrentPasswordIncorrect | Change-password screen text for a server `InvalidCredentials`. |
