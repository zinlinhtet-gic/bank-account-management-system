# Message Codes

Codes are stable once released. The WPF client mirrors the server codes below 6000 in
`bams.desktop/Utils/MessageCode.cs` with the same numbers; client-only codes use 6000+.

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 1000 | Success | 200 | General successful operation. |
| 1100 | AccountCreatedSuccessfully | 201 | Account creation succeeded. |
| 3000 | ValidationFailed | 400 | One or more validation errors occurred. |
| 3001 | RequiredFieldMissing | 400 | A required field is empty. |
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

## Number blocks per feature

Duplicate enum values compile without error, so each feature takes numbers only from its own block.

| Feature                         | Success   | Validation | Not Found | Conflict  | Business rule |
| ------------------------------- | --------- | ---------- | --------- | --------- | ------------- |
| Common / Auth / Users           | 1000-1099 | 3000-3099  | 4200-4204 | 4300-4309 | 4400          |
| Accounts (incl. fixed deposits) | 1100-1199 | 3100-3199  | 4205-4229 | 4310-4319 | 4401-4429     |
| Transactions / history          | 1200-1299 | 3300-3399  | 4240-4249 | 4330-4339 | 4440-4459     |
| Customers / KYC                 | 1300-1399 | 3200-3299  | 4230-4239 | 4320-4329 | 4430-4439     |
| Accounting / Operations / Config / Audit | 1400-1499 | 3400-3499 | 4250-4259 | 4340-4349 | 4460-4479 |

Core "not found" codes 4200-4204 (Resource, Account, Customer, AccountType, User) are shared by all features. Authentication (4000-4099) and
authorization (4100-4199) codes are added only by the permission owner.
| 1000 | Success | General successful operation. |
| 1100 | AccountCreatedSuccessfully | Account creation succeeded. |
| 1101 | AccountStatusUpdatedSuccessfully | Account status update succeeded. |
| 1102 | AccountBalanceUpdatedSuccessfully | Account balance adjustment succeeded. |
| 1103 | AccountHoldersUpdatedSuccessfully | Joint-account holder update succeeded. |
| 1104 | FixedDepositUpdatedSuccessfully | Fixed-deposit update succeeded. |
| 3000 | ValidationFailed | One or more validation errors occurred. |
| 3002 | InvalidRequest | The request shape or content is invalid. |
| 3003 | InvalidAmount | A supplied amount is invalid. |
| 3100 | AccountNameRequired | Account name is missing. |
| 3101 | AccountTypeInvalid | Account type is invalid. |
| 3102 | OpeningBalanceInvalid | Opening balance is below the allowed minimum. |
| 3103 | HolderAlreadyHasActiveAccount | The customer already has an active individual account of the requested type. |
| 3104 | SharedAccountRequiresTwoHolders | A shared account request does not provide two holder NRCs. |
| 3105 | SharedAccountRequiresTwoOwnershipPercentages | A shared account request does not provide a percentage for each holder. |
| 3106 | OwnershipPercentageOutOfRange | An ownership percentage is not greater than zero and no greater than 100. |
| 3107 | SharedAccountOwnershipPercentagesMustSumTo100 | Shared account ownership percentages do not total 100. |
| 3108 | RequiredAccountDocumentMissing | One or more documents required by the account type are missing. |
| 3109 | DuplicateAccountDocumentType | More than one file was supplied for the same document type. |
| 3110 | UploadedFileEmpty | An uploaded document has no content. |
| 3111 | UploadedFileTooLarge | An uploaded document exceeds the configured size limit. |
| 3112 | UnsupportedDocumentFileType | An uploaded file is not a supported PDF, JPEG, or PNG. |
| 3113 | InvalidDocumentFileContent | File content does not match the declared document format. |
| 3114 | InvalidDocumentFileReference | A file reference resolves outside private storage. |
| 3115 | UnsupportedAccountDocumentType | An undefined account document type was supplied. |
| 3116 | UploadedFileNameTooLong | An uploaded document filename exceeds the metadata limit. |
| 3117 | AccountDocumentNumberTooLong | A document number exceeds the metadata limit. |
| 3118 | AccountStatusInvalid | The requested account status is not defined. |
| 3119 | AccountBalanceCannotBeNegative | An account balance update would result in a negative balance. |
| 3120 | CustomerDoesNotHaveRequiredProducts | The customer does not hold the product required for the requested account type. |
| 3121 | AccountHolderSelectionInvalid | A holder update does not contain the account's two distinct holder relationships. |
| 3122 | AccountHolderSigningRuleTooLong | A shared signing rule exceeds its maximum permitted length. |
| 3123 | SharedAccountRequiresExactlyOnePrimaryHolder | A shared holder update does not specify exactly one primary holder. |
| 3124 | FixedDepositRequestInvalid | Fixed-deposit creation fields are missing or supplied for a non-fixed account. |
| 3125 | FixedDepositCurrentPrincipalInvalid | A fixed-deposit principal update is negative. |
| 3126 | FixedDepositUpdateRequiresChanges | A fixed-deposit update contains no fields to change. |
| 4100 | AccessDenied | The user is not allowed to perform the operation. |
| 4201 | AccountNotFound | The requested account does not exist. |
| 4202 | CustomerNotFound | No customer exists for the provided NRC. |
| 4203 | AccountTypeNotFound | The requested account type does not exist. |
| 4204 | FixedDepositNotFound | The requested fixed deposit does not exist. |
| 4205 | InterestRateRuleNotFound | The requested interest-rate rule does not exist. |
| 4206 | PayoutAccountNotFound | The supplied payout account is not valid for the primary holder. |
| 4207 | RequiredPayoutAccountNotFound | The primary holder has no active individual account of the required type. |
| 4302 | AccountAlreadyExists | An account conflicts with an existing account. |
| 4400 | BusinessRuleViolation | A business rule was violated. |
| 4401 | AccountTypeIdentifierOutOfRange | The account type identifier cannot fit the two-digit account-number segment. |
| 4402 | AccountNumberSequenceExhausted | The account type has used all account-number sequences for the current UTC hour. |
| 4403 | AccountStatusTransitionNotAllowed | The requested account status transition is not allowed. |
| 4404 | AccountHolderUpdateNotAllowed | Holder details cannot be updated for the specified account type or status. |
| 4405 | InterestRateRuleNotApplicable | The selected interest-rate rule does not apply to the fixed deposit. |
| 4406 | RequiredPayoutAccountNotConfigured | The fixed account type has no required payout product configured. |
| 4407 | FixedDepositStatusTransitionNotAllowed | The requested fixed-deposit status transition or update is forbidden. |
| 5000 | InternalServerError | An unexpected server error occurred. |
| 5001 | FileStorageFailed | A validated document could not be written to private storage. |
