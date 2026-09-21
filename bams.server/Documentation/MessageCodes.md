# Message Codes

| Code | Name | Meaning |
| --- | --- | --- |
| 1000 | Success | General successful operation. |
| 1100 | AccountCreatedSuccessfully | Account creation succeeded. |
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
| 4100 | AccessDenied | The user is not allowed to perform the operation. |
| 4201 | AccountNotFound | The requested account does not exist. |
| 4202 | CustomerNotFound | No customer exists for the provided NRC. |
| 4203 | AccountTypeNotFound | The requested account type does not exist. |
| 4302 | AccountAlreadyExists | An account conflicts with an existing account. |
| 4400 | BusinessRuleViolation | A business rule was violated. |
| 4401 | AccountTypeIdentifierOutOfRange | The account type identifier cannot fit the two-digit account-number segment. |
| 4402 | AccountNumberSequenceExhausted | The account type has used all account-number sequences for the current UTC hour. |
| 5000 | InternalServerError | An unexpected server error occurred. |
| 5001 | FileStorageFailed | A validated document could not be written to private storage. |
