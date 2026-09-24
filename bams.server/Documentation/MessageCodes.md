# Message Codes

Message codes are stable API contracts. Clients must branch on the numeric code or enum name, never on message text. `Messages/MessageCode.cs` is the source of truth for server codes.

## Code ranges

| Range | Category |
| --- | --- |
| 1000–1999 | Success |
| 3000–3999 | Validation |
| 4000–4099 | Authentication |
| 4100–4199 | Authorization |
| 4200–4299 | Not found |
| 4300–4399 | Conflict |
| 4400–4499 | Business rule |
| 5000–5999 | System or infrastructure |
| 6000+ | Desktop-client only |

## Success

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 1000 | Success | 200 | General successful operation. |
| 1100 | AccountCreatedSuccessfully | 201 | Account creation succeeded. |
| 1101 | AccountStatusUpdatedSuccessfully | 200 | Account status update succeeded. |
| 1102 | AccountBalanceUpdatedSuccessfully | 200 | Account balance adjustment succeeded. |
| 1103 | AccountHoldersUpdatedSuccessfully | 200 | Joint-account holder update succeeded. |
| 1104 | FixedDepositUpdatedSuccessfully | 200 | Fixed-deposit update succeeded. |

## Validation

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 3000 | ValidationFailed | 400 | One or more validation errors occurred. |
| 3001 | RequiredFieldMissing | 400 | A required field is empty. |
| 3002 | InvalidRequest | 400 | The request shape or content is invalid. |
| 3003 | InvalidAmount | 400 | A supplied amount is invalid. |
| 3004 | InvalidCredentials | 400 | Login or current-password credentials are incorrect. |
| 3005 | PasswordDoesNotMeetRequirements | 400 | The new password fails complexity requirements. |
| 3102 | OpeningBalanceInvalid | 400 | Opening balance is below the product minimum. |
| 3103 | HolderAlreadyHasActiveAccount | 400 | The customer already has an active individual account of this product. |
| 3104 | SharedAccountRequiresTwoHolders | 400 | A shared account does not provide two holders. |
| 3105 | SharedAccountRequiresTwoOwnershipPercentages | 400 | A shared account does not provide both percentages. |
| 3106 | OwnershipPercentageOutOfRange | 400 | A holder percentage is outside the permitted range. |
| 3107 | SharedAccountOwnershipPercentagesMustSumTo100 | 400 | Joint percentages do not total 100. |
| 3108 | RequiredAccountDocumentMissing | 400 | A required account document is missing. |
| 3109 | DuplicateAccountDocumentType | 400 | A document type was supplied more than once. |
| 3110 | UploadedFileEmpty | 400 | An uploaded document is empty. |
| 3111 | UploadedFileTooLarge | 400 | An uploaded document exceeds the size limit. |
| 3112 | UnsupportedDocumentFileType | 400 | The document is not PDF, JPEG, or PNG. |
| 3113 | InvalidDocumentFileContent | 400 | File content does not match its declared type. |
| 3114 | InvalidDocumentFileReference | 400 | A file reference resolves outside private storage. |
| 3115 | UnsupportedAccountDocumentType | 400 | The document type is undefined. |
| 3116 | UploadedFileNameTooLong | 400 | The original filename exceeds its limit. |
| 3117 | AccountDocumentNumberTooLong | 400 | The document number exceeds its limit. |
| 3118 | AccountStatusInvalid | 400 | The requested account status is undefined. |
| 3119 | AccountBalanceCannotBeNegative | 400 | A balance update would produce a negative balance. |
| 3120 | CustomerDoesNotHaveRequiredProducts | 400 | A holder lacks a product required by the selected account type. |
| 3121 | AccountHolderSelectionInvalid | 400 | A holder update does not contain both existing holders. |
| 3122 | AccountHolderSigningRuleTooLong | 400 | The signing rule exceeds its limit. |
| 3123 | SharedAccountRequiresExactlyOnePrimaryHolder | 400 | A joint account does not identify exactly one primary holder. |
| 3124 | FixedDepositRequestInvalid | 400 | Fixed-deposit fields are incomplete or supplied for another product. |
| 3125 | FixedDepositCurrentPrincipalInvalid | 400 | Current principal is negative. |
| 3126 | FixedDepositUpdateRequiresChanges | 400 | No editable fixed-deposit field was supplied. |
| 3127 | AccountCursorInvalid | 400 | The pagination cursor is malformed or unsupported. |

## Authentication and authorization

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 4000 | AuthenticationRequired | 401 | No valid JWT exists or its user no longer exists. |
| 4001 | PasswordChangeRequired | N/A | The user must change their password. |
| 4002 | PasswordChangedSuccessfully | 200 | Password change succeeded. |
| 4003 | UserAccountDisabled | 403 | The staff user is disabled. |
| 4100 | AccessDenied | 403 | The operation is not allowed. |
| 4101 | InsufficientPermission | 403 | The user lacks the required permission. |

## Not found

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 4200 | ResourceNotFound | 404 | A generic resource was not found. |
| 4201 | AccountNotFound | 404 | The account does not exist. |
| 4202 | CustomerNotFound | 404 | The customer does not exist. |
| 4203 | AccountTypeNotFound | 404 | The account type does not exist. |
| 4204 | UserNotFound | 404 | The staff user does not exist. |
| 4205 | FixedDepositNotFound | 404 | The fixed deposit does not exist. |
| 4206 | InterestRateRuleNotFound | 404 | The interest-rate rule does not exist. |
| 4207 | PayoutAccountNotFound | 404 | The supplied payout account is ineligible or absent. |
| 4208 | RequiredPayoutAccountNotFound | 404 | The primary holder lacks the required payout product. |

## Conflict

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 4304 | ConcurrentModification | 409 | A versioned record changed after the caller read it. |
| 4305 | UsernameAlreadyExists | 409 | Another user has the username. |
| 4306 | EmailAlreadyExists | 409 | Another user has the email address. |

## Business rules

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 4400 | BusinessRuleViolation | 422 | A general business rule was violated. |
| 4401 | AccountTypeIdentifierOutOfRange | 422 | The account-type ID cannot fit its account-number segment. |
| 4402 | AccountNumberSequenceExhausted | 422 | The hourly product sequence is exhausted. |
| 4403 | AccountStatusTransitionNotAllowed | 422 | The account status transition is forbidden. |
| 4404 | AccountHolderUpdateNotAllowed | 422 | Holder details cannot be updated for this account. |
| 4405 | InterestRateRuleNotApplicable | 422 | The selected interest-rate rule is inapplicable. |
| 4406 | RequiredPayoutAccountNotConfigured | 422 | The fixed-deposit product has no required payout product. |
| 4407 | FixedDepositStatusTransitionNotAllowed | 422 | The fixed-deposit transition or update is forbidden. |

## System and infrastructure

| Code | Name | HTTP | Meaning |
| --- | --- | --- | --- |
| 5000 | InternalServerError | 500 | An unexpected server error occurred. |
| 5001 | FileStorageFailed | 500 | A validated document could not be stored. |

## Desktop-client-only codes

The WPF client mirrors server codes below 6000 in `bams.desktop/Utils/MessageCode.cs`. These additional codes never come from the server.

| Code | Name | Meaning |
| --- | --- | --- |
| 6000 | ClientError | Unexpected client-side failure. |
| 6001 | NetworkUnavailable | The server could not be reached. |
| 6002 | RequestTimeout | The server did not answer in time. |
| 6003 | InvalidServerResponse | The server response could not be read. |
| 6010 | CurrentPasswordIncorrect | Client text corresponding to server `InvalidCredentials`. |
