# Business Rules

## Accounts

- Account names are required and capped by `AccountConstants.AccountNameMaximumLength`.
- Opening balances cannot be below `AccountConstants.MinimumOpeningBalance`.
- Account types must be valid `AccountType` enum values.
- New accounts are created with `AccountStatus.Active`.
- Account numbers are generated server-side using `AccountConstants.AccountNumberPrefix`.

## Customers

- Full name is required (leading/trailing whitespace is trimmed before saving).
- Date of birth cannot be in the future and the customer must be at least `CustomerConstants.MinimumAgeYears` old.
- Citizen customers (`CustomerType.Citizen`) require an NRC number; foreigner customers (`CustomerType.Foreigner`) require a passport number.
- When supplied, the customer email must match `CustomerConstants.CustomerEmailRegexPattern`.
- A new customer cannot share an NRC number, passport number, or email with an existing customer.
- New customers are created with `RiskLevel.Low`, `KycStatus.Pending`, and status `CustomerConstants.DefaultStatus` ("Inactive" until KYC is verified).
- Customer numbers are sequential, formatted as `CustomerConstants.CustomerNumberPrefix` + an 8-digit running sequence (e.g. `CUS00000001`), computed by `CustomerNumberGenerator` from the highest existing customer number.
- A create-customer request may include zero or more `CustomerDocument` entries (NRC, passport, proof of address, etc.), each with an optional uploaded file. Documents no longer track their own verification status — a document is considered verified once `VerifiedAt`/`VerifiedBy` are set (see `CustomerDocument`); until then it is implicitly pending.
- Customer creation, its documents, and the database save all happen inside one database transaction (`CustomerService.CreateCustomerAsync`). Uploaded files are written to disk before the transaction commits; if the transaction fails for any reason, any files already written are deleted so storage does not accumulate orphaned uploads.
- Uploaded document files must be non-empty (`IFileStorageService.SaveAsync` rejects empty files with `MessageCode.CustomerDocumentFileEmpty`).
