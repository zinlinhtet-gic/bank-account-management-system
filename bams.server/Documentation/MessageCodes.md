# Message Codes

Codes are stable once released. The WPF client mirrors the server codes below 6000 in
`bams.desktop/Utils/MessageCode.cs` with the same numbers; client-only codes use 6000+.

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 1000 | Success | 200 | General successful operation. |
| 1001 | UserCreatedSuccessfully | 201 | A staff user was created (User Management). |
| 1002 | UserUpdatedSuccessfully | 200 | A staff user was updated. |
| 1003 | UserPasswordResetSuccessfully | 200 | A user's password was reset to their role's default. |
| 1004 | UserDeletedSuccessfully | — | A user was soft-deleted (the endpoint returns 204; client-side text). |
| 1100 | AccountCreatedSuccessfully | 201 | Account creation succeeded. |
| 1200 | TransactionCompletedSuccessfully | 200 | A deposit, withdrawal, internal transfer or NRC pickup was posted. |
| 1201 | TransactionSubmittedSuccessfully | 200 | An interbank or NRC transfer was accepted and is pending. |
| 1202 | TransactionRefundedSuccessfully | 200 | An NRC transfer was cancelled or an interbank transfer failed; the sender was refunded. |
| 1203 | InterbankTransferSettledSuccessfully | 200 | A pending interbank transfer was recorded as settled. |
| 3000 | ValidationFailed | 400 | One or more validation errors occurred. |
| 3001 | RequiredFieldMissing | 400 | A required field is empty. |
| 3002 | InvalidRequest | 400 | The request shape or content is invalid. |
| 3003 | InvalidAmount | 400 | A supplied amount is invalid. |
| 3004 | InvalidCredentials | 400 | Username or password is wrong (login), or the current password is wrong (change password). |
| 3005 | PasswordDoesNotMeetRequirements | 400 | New password fails the complexity rules. |
| 3006 | InvalidEmailFormat | 400 | Email is not in the form name@domain.tld. |
| 3007 | InvalidUsernameFormat | 400 | Username is not 3-64 letters, numbers, dot, underscore or hyphen. |
| 3008 | InvalidPhoneFormat | 400 | Phone contains characters other than digits, +, spaces, hyphens or brackets, or has the wrong length. |
| 3009 | InvalidRole | 400 | The role does not exist or cannot be assigned in User Management. |
| 3010 | FieldTooLong | 400 | A field is longer than its column allows. |
| 3011 | InvalidDateRange | 400 | A "from" date is after the "to" date in a list filter. |
| 3012 | NewPasswordSameAsCurrent | 400 | Change password: the new password equals the current one. |
| 3013 | DefaultPasswordNotAllowed | 400 | Change password: the new password is one of the role default passwords. |
| 3102 | OpeningBalanceInvalid | 400 | Opening balance is below the account type's minimum. |
| 3300 | SameSourceAndDestinationAccount | 400 | A transfer names the same account as source and destination. |
| 3301 | InvalidPickupCode | 400 | The NRC pickup code does not match. |
| 4000 | AuthenticationRequired | 401 | No valid JWT, or the token's user no longer exists. |
| 4001 | PasswordChangeRequired | — | The user must change their password before continuing. |
| 4002 | PasswordChangedSuccessfully | 200 | Password change succeeded. |
| 4003 | UserAccountDisabled | 403 | The user's `Status` is `Disabled`; login and protected endpoints are refused. |
| 4004 | UserAccountDeleted | 403 | The user was soft-deleted; login and protected endpoints are refused. |
| 4100 | AccessDenied | 403 | The user is not allowed to perform the operation. |
| 4101 | InsufficientPermission | 403 | None of the user's roles grants a permission required by `[RequirePermission]`. |
| 4200 | ResourceNotFound | 404 | A generic resource was not found. |
| 4201 | AccountNotFound | 404 | The requested bank account does not exist. |
| 4203 | AccountTypeNotFound | 404 | The requested account type does not exist. |
| 4204 | UserNotFound | 404 | The requested staff user does not exist. |
| 4240 | TransactionNotFound | 404 | The requested transaction (e.g. an NRC transfer for pickup) does not exist. |
| 4241 | OtherBankNotFound | 404 | The destination bank of an interbank transfer, or an NRC pickup bank, does not exist. |
| 4242 | BranchNotFound | 404 | The NRC pickup branch does not exist or is not active. |
| 4305 | UsernameAlreadyExists | 409 | Another user already has this username. |
| 4306 | EmailAlreadyExists | 409 | Another user already has this email. |
| 4330 | IdempotencyKeyReused | 409 | The `Idempotency-Key` was already used for a different user, transaction type or amount. |
| 4400 | BusinessRuleViolation | 422 | A business rule was violated. |
| 4440 | InsufficientBalance | 422 | A debit exceeds the account's available balance. |
| 4441 | AccountNotOperational | 422 | The account is closed, frozen or suspended. |
| 4442 | PickupCodeExpired | 422 | The NRC pickup code has passed its expiry time. |
| 4443 | TransactionNotPendingPickup | 422 | The NRC transfer was already picked up or is no longer pending. |
| 4444 | WithdrawalNotAllowed | 422 | The account type does not allow withdrawals. |
| 4445 | TransferNotAllowed | 422 | The source account type does not allow transfers. |
| 4446 | MinimumBalanceRequired | 422 | The debit would leave less than the account type's minimum maintained balance. |
| 4447 | DailyTransactionLimitExceeded | 422 | The debit would exceed the account type's daily limit. |
| 4448 | MonthlyTransactionLimitExceeded | 422 | The debit would exceed the account type's monthly limit. |
| 4449 | PickupAttemptsExceeded | 422 | Too many wrong pickup codes; the NRC transfer can only be cancelled. |
| 4450 | TransactionNotPending | 422 | The interbank transfer's gateway result was already recorded. |
| 4451 | NrcPickupLocationMismatch | 422 | Pickup was tried on an other-bank NRC transfer, or a payout recorded for a branch one. |
| 4480 | LastManagerCannotBeRemoved | 422 | Deleting, or taking the manager role from, the only active manager is refused. |
| 5000 | InternalServerError | 500 | An unexpected server error occurred. |

## Client-only codes (WPF)

| Code | Name | Meaning |
| --- | --- | --- |
| 6000 | ClientError | Unexpected client-side failure. |
| 6001 | NetworkUnavailable | The server could not be reached. |
| 6002 | RequestTimeout | The server did not answer in time. |
| 6003 | InvalidServerResponse | The server response could not be read. |
| 6010 | CurrentPasswordIncorrect | Change-password screen text for a server `InvalidCredentials`. |

## Number blocks per feature

Duplicate enum values compile without error, so each feature takes numbers only from its own block.

| Feature                         | Success   | Validation | Not Found | Conflict  | Business rule |
| ------------------------------- | --------- | ---------- | --------- | --------- | ------------- |
| Common / Auth / Users           | 1000-1099 | 3000-3099  | 4200-4204 | 4300-4309 | 4400, 4480-4489 |
| Accounts (incl. fixed deposits) | 1100-1199 | 3100-3199  | 4205-4229 | 4310-4319 | 4401-4429     |
| Transactions / history          | 1200-1299 | 3300-3399  | 4240-4249 | 4330-4339 | 4440-4459     |
| Customers / KYC                 | 1300-1399 | 3200-3299  | 4230-4239 | 4320-4329 | 4430-4439     |
| Accounting / Operations / Config / Audit | 1400-1499 | 3400-3499 | 4250-4259 | 4340-4349 | 4460-4479 |

Core "not found" codes 4200-4204 (Resource, Account, Customer, AccountType, User) are shared by all features. Authentication (4000-4099) and
authorization (4100-4199) codes are added only by the permission owner.
