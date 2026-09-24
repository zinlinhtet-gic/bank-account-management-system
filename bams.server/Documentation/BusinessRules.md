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
- Updating a customer (`PATCH /api/customers/{id}`) is a partial/merge update: only properties supplied (non-null) in the request are changed; omitted properties keep their current value. The same field validation and NRC/passport/email uniqueness rules as creation apply to the merged (existing + supplied) values, and the uniqueness check excludes the customer being updated.
- An update request may also add and/or edit documents in the same call: a `Documents` entry with an `Id` edits that existing document (only its supplied fields change, and a supplied file replaces the stored one); an entry without an `Id` adds a new document and must specify `DocumentType` (`MessageCode.CustomerDocumentTypeRequired` otherwise). An `Id` that doesn't belong to the customer fails with `MessageCode.CustomerDocumentNotFound`.
- When a document's file is replaced during an update, the old file on disk is only deleted after the database save commits successfully, so a failed update never leaves a document pointing at a file that no longer exists.
- KYC review (`POST /api/customers/{id}/kyc-review`) sets `Customer.KycStatus` to `Verified` or `Rejected` — `Pending` is not a valid review outcome (`MessageCode.InvalidKycReviewStatus`). The reviewing user must exist (`MessageCode.KycReviewerNotFound` otherwise) and must hold the `RoleConstants.Manager` role (`MessageCode.AccessDenied` via `ForbiddenException` otherwise — only a Branch Manager may review KYC). No new fields were added to `Customer` for this: approving (`Verified`) stamps `VerifiedAt`/`VerifiedBy` on every one of the customer's existing `CustomerDocument` records using the already-existing per-document fields; rejecting only changes `Customer.KycStatus` and leaves documents untouched.

## Security (Users / Roles)

- Three roles exist: `RoleConstants.Manager` ("Branch Manager"), `RoleConstants.Officer`, `RoleConstants.Auditor`.
- In Development only, `SecuritySeeder` seeds these three roles and one example user per role (`manager1`, `officer1`, `auditor1`) with a placeholder (non-real) password hash, if the `Roles`/`Users` tables are empty. It never runs, and never overwrites existing data, outside `IsDevelopment()`.
- No authentication is implemented yet, so role-restricted endpoints (like KYC review) take the acting user's Id directly in the request body rather than reading it from an authenticated session.

## Users

- Every user has a `Status` (`UserStatus.Active` or `UserStatus.Disabled`); new users default to `Active`.
- A disabled user cannot log in, fetch permissions, change their password, or call any `[RequirePermission]` endpoint,
  even with an unexpired token. These requests fail with `UserAccountDisabled` (403).
- On login, the disabled check runs only after the password is verified, so account state is not revealed to callers
  who do not know the password.
