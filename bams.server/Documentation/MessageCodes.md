# Message Codes

Codes are stable once released. The WPF client mirrors the server codes below 6000 in
`bams.desktop/Utils/MessageCode.cs` with the same numbers; client-only codes use 6000+.

| Code | Name                            | HTTP | Meaning                                                                                    |
| ---- | ------------------------------- | ---- | ------------------------------------------------------------------------------------------ |
| 1000 | Success                         | 200  | General successful operation.                                                              |
| 1100 | AccountCreatedSuccessfully      | 201  | Account creation succeeded.                                                                |
| 3000 | ValidationFailed                | 400  | One or more validation errors occurred.                                                    |
| 3002 | InvalidRequest                  | 400  | The request shape or content is invalid.                                                   |
| 3003 | InvalidAmount                   | 400  | A supplied amount is invalid.                                                              |
| 3004 | InvalidCredentials              | 400  | Username or password is wrong (login), or the current password is wrong (change password). |
| 3005 | PasswordDoesNotMeetRequirements | 400  | New password fails the complexity rules.                                                   |
| 3102 | OpeningBalanceInvalid           | 400  | Opening balance is below the account type's minimum.                                       |
| 4000 | AuthenticationRequired          | 401  | No valid JWT, or the token's user no longer exists.                                        |
| 4001 | PasswordChangeRequired          | —    | The user must change their password before continuing.                                     |
| 4002 | PasswordChangedSuccessfully     | 200  | Password change succeeded.                                                                 |
| 4003 | UserAccountDisabled             | 403  | The user's `Status` is not `Active`; login and protected endpoints are refused.            |
| 4100 | AccessDenied                    | 403  | The user is not allowed to perform the operation.                                          |
| 4101 | InsufficientPermission          | 403  | None of the user's roles grants a permission required by `[RequirePermission]`.            |
| 4200 | ResourceNotFound                | 404  | A generic resource was not found.                                                          |
| 4201 | AccountNotFound                 | 404  | The requested bank account does not exist.                                                 |
| 4203 | AccountTypeNotFound             | 404  | The requested account type does not exist.                                                 |
| 4204 | UserNotFound                    | 404  | The requested staff user does not exist.                                                   |
| 4305 | UsernameAlreadyExists           | 409  | Another user already has this username.                                                    |
| 4306 | EmailAlreadyExists              | 409  | Another user already has this email.                                                       |
| 4400 | BusinessRuleViolation           | 422  | A business rule was violated.                                                              |
| 5000 | InternalServerError             | 500  | An unexpected server error occurred.                                                       |

## Client-only codes (WPF)

| Code | Name                            | Meaning                                                                        |
| ---- | ------------------------------- | ------------------------------------------------------------------------------ |
| 1000 | Success                         | General successful operation.                                                  |
| 1100 | AccountCreatedSuccessfully      | Account creation succeeded.                                                    |
| 1300 | CustomerCreatedSuccessfully     | Customer creation succeeded.                                                   |
| 1301 | CustomerUpdatedSuccessfully     | Customer update succeeded.                                                     |
| 1302 | CustomerKycReviewedSuccessfully | Customer KYC review decision recorded successfully.                            |
| 3000 | ValidationFailed                | One or more validation errors occurred.                                        |
| 3002 | InvalidRequest                  | The request shape or content is invalid.                                       |
| 3003 | InvalidAmount                   | A supplied amount is invalid.                                                  |
| 3100 | AccountNameRequired             | Account name is missing.                                                       |
| 3101 | AccountTypeInvalid              | Account type is invalid.                                                       |
| 3102 | OpeningBalanceInvalid           | Opening balance is below the allowed minimum.                                  |
| 3200 | CustomerFullNameRequired        | Customer full name is missing.                                                 |
| 3201 | CustomerDateOfBirthInvalid      | Customer date of birth is in the future.                                       |
| 3202 | CustomerBelowMinimumAge         | Customer does not meet the minimum age requirement.                            |
| 3203 | NrcNumberRequired               | NRC number is required for citizen customers.                                  |
| 3204 | PassportNumberRequired          | Passport number is required for foreigner customers.                           |
| 3205 | CustomerDocumentFileEmpty       | An uploaded customer document file is empty.                                   |
| 3206 | CustomerEmailInvalid            | The supplied customer email address is not a valid format.                     |
| 3207 | CustomerDocumentTypeRequired    | A new document entry (no Id) must specify a document type.                     |
| 3208 | InvalidKycReviewStatus          | A KYC review must set the status to Verified or Rejected, not Pending.         |
| 4100 | AccessDenied                    | The user is not allowed to perform the operation.                              |
| 4201 | AccountNotFound                 | The requested account does not exist.                                          |
| 4202 | CustomerNotFound                | The requested customer does not exist.                                         |
| 4204 | CustomerDocumentNotFound        | A document Id in an update request does not belong to the customer.            |
| 4205 | KycReviewerNotFound             | The user performing a KYC review does not exist.                               |
| 4302 | AccountAlreadyExists            | An account conflicts with an existing account.                                 |
| 4305 | CustomerAlreadyExists           | A customer with the same NRC number, passport number, or email already exists. |
| 4400 | BusinessRuleViolation           | A business rule was violated.                                                  |
| 5000 | InternalServerError             | An unexpected server error occurred.                                           |
| 6000 | ClientError                     | Unexpected client-side failure.                                                |
| 6001 | NetworkUnavailable              | The server could not be reached.                                               |
| 6002 | RequestTimeout                  | The server did not answer in time.                                             |
| 6003 | InvalidServerResponse           | The server response could not be read.                                         |
| 6010 | CurrentPasswordIncorrect        | Change-password screen text for a server `InvalidCredentials`.                 |
