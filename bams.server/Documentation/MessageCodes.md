# Message Codes

| Code | Name | Meaning |
| --- | --- | --- |
| 1000 | Success | General successful operation. |
| 1100 | AccountCreatedSuccessfully | Account creation succeeded. |
| 1300 | CustomerCreatedSuccessfully | Customer creation succeeded. |
| 3000 | ValidationFailed | One or more validation errors occurred. |
| 3002 | InvalidRequest | The request shape or content is invalid. |
| 3003 | InvalidAmount | A supplied amount is invalid. |
| 3100 | AccountNameRequired | Account name is missing. |
| 3101 | AccountTypeInvalid | Account type is invalid. |
| 3102 | OpeningBalanceInvalid | Opening balance is below the allowed minimum. |
| 3200 | CustomerFullNameRequired | Customer full name is missing. |
| 3201 | CustomerDateOfBirthInvalid | Customer date of birth is in the future. |
| 3202 | CustomerBelowMinimumAge | Customer does not meet the minimum age requirement. |
| 3203 | NrcNumberRequired | NRC number is required for citizen customers. |
| 3204 | PassportNumberRequired | Passport number is required for foreigner customers. |
| 3205 | CustomerDocumentFileEmpty | An uploaded customer document file is empty. |
| 3206 | CustomerEmailInvalid | The supplied customer email address is not a valid format. |
| 4100 | AccessDenied | The user is not allowed to perform the operation. |
| 4201 | AccountNotFound | The requested account does not exist. |
| 4202 | CustomerNotFound | The requested customer does not exist. |
| 4302 | AccountAlreadyExists | An account conflicts with an existing account. |
| 4305 | CustomerAlreadyExists | A customer with the same NRC number, passport number, or email already exists. |
| 4400 | BusinessRuleViolation | A business rule was violated. |
| 5000 | InternalServerError | An unexpected server error occurred. |
