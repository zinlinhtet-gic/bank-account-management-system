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
| ---- | ------------------------------- | ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------ |
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
| Code | Name                            | HTTP                                                                           | Meaning                                                                                    |
| ---  | ---                             | ---                                                                            | ---                                                                                        |
| 1000 | Success                         | 200                                                                            | General successful operation.                                                              |
| 1100 | AccountCreatedSuccessfully      | 201                                                                            | Account creation succeeded.                                                                |
| 3000 | ValidationFailed                | 400                                                                            | One or more validation errors occurred.                                                    |
| 3001 | RequiredFieldMissing            | 400                                                                            | A required field is empty.                                                                 |
| 3002 | InvalidRequest                  | 400                                                                            | The request shape or content is invalid.                                                   |
| 3003 | InvalidAmount                   | 400                                                                            | A supplied amount is invalid.                                                              |
| 3004 | InvalidCredentials              | 400                                                                            | Username or password is wrong (login), or the current password is wrong (change password). |
| 3005 | PasswordDoesNotMeetRequirements | 400                                                                            | New password fails the complexity rules.                                                   |
| 3102 | OpeningBalanceInvalid           | 400                                                                            | Opening balance is below the account type's minimum.                                       |
| 4000 | AuthenticationRequired          | 401                                                                            | No valid JWT, or the token's user no longer exists.                                        |
| 4001 | PasswordChangeRequired          | —                                                                              | The user must change their password before continuing.                                     |
| 4002 | PasswordChangedSuccessfully     | 200                                                                            | Password change succeeded.                                                                 |
| 4003 | UserAccountDisabled             | 403                                                                            | The user's `Status` is not `Active`; login and protected endpoints are refused.            |
| 4100 | AccessDenied                    | 403                                                                            | The user is not allowed to perform the operation.                                          |
| 4101 | InsufficientPermission          | 403                                                                            | None of the user's roles grants a permission required by `[RequirePermission]`.            |
| 4200 | ResourceNotFound                | 404                                                                            | A generic resource was not found.                                                          |
| 4201 | AccountNotFound                 | 404                                                                            | The requested bank account does not exist.                                                 |
| 4203 | AccountTypeNotFound             | 404                                                                            | The requested account type does not exist.                                                 |
| 4204 | UserNotFound                    | 404                                                                            | The requested staff user does not exist.                                                   |
| 4305 | UsernameAlreadyExists           | 409                                                                            | Another user already has this username.                                                    |
| 4306 | EmailAlreadyExists              | 409                                                                            | Another user already has this email.                                                       |
| 4400 | BusinessRuleViolation           | 422                                                                            | A business rule was violated.                                                              |
| 5000 | InternalServerError             | 500                                                                            | An unexpected server error occurred.                                                       |

## Client-only codes (WPF)

| Code | Name                     | Meaning                                                        |
| ---- | ------------------------ | -------------------------------------------------------------- |
| 6000 | ClientError              | Unexpected client-side failure.                                |
| 6001 | NetworkUnavailable       | The server could not be reached.                               |
| 6002 | RequestTimeout           | The server did not answer in time.                             |
| 6003 | InvalidServerResponse    | The server response could not be read.                         |
| 6010 | CurrentPasswordIncorrect | Change-password screen text for a server `InvalidCredentials`. |

## Number blocks per feature

Duplicate enum values compile without error, so each feature takes numbers only from its own block.

| Feature                                  | Success   | Validation | Not Found | Conflict  | Business rule |
| ---------------------------------------- | --------- | ---------- | --------- | --------- | ------------- |
| Common / Auth / Users                    | 1000-1099 | 3000-3099  | 4200-4204 | 4300-4309 | 4400          |
| Accounts (incl. fixed deposits)          | 1100-1199 | 3100-3199  | 4205-4229 | 4310-4319 | 4401-4429     |
| Transactions / history                   | 1200-1299 | 3300-3399  | 4240-4249 | 4330-4339 | 4440-4459     |
| Customers / KYC                          | 1300-1399 | 3200-3299  | 4230-4239 | 4320-4329 | 4430-4439     |
| Accounting / Operations / Config / Audit | 1400-1499 | 3400-3499  | 4250-4259 | 4340-4349 | 4460-4479     |

Core "not found" codes 4200-4204 (Resource, Account, Customer, AccountType, User) are shared by all features. Authentication (4000-4099) and
authorization (4100-4199) codes are added only by the permission owner.
