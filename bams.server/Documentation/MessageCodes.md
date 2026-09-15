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
| 4100 | AccessDenied | The user is not allowed to perform the operation. |
| 4201 | AccountNotFound | The requested account does not exist. |
| 4302 | AccountAlreadyExists | An account conflicts with an existing account. |
| 4400 | BusinessRuleViolation | A business rule was violated. |
| 5000 | InternalServerError | An unexpected server error occurred. |
