# Business Rules

## Accounts

- Account names are required and capped by `AccountConstants.AccountNameMaximumLength`.
- Opening balances cannot be below `AccountConstants.MinimumOpeningBalance`.
- Account types must be valid `AccountType` enum values.
- New accounts are created with `AccountStatus.Active`.
- Account numbers are generated server-side using `AccountConstants.AccountNumberPrefix`.
- A customer cannot hold more than one active individual account of the same account type.
- Individual accounts require one registered customer NRC.
- Shared accounts require two registered customer NRCs and create joint ownership records for both customers.
- Individual account holders are stored with 100 percent ownership.
- Joint holder percentages must each be greater than zero, no greater than 100, and total exactly 100 percent.
- Account numbers are generated server-side as 16 digits in `TTyyyyMMddHHSSSS` format.
- `TT` is the two-digit account type identifier, `yyyyMMddHH` is the UTC generation hour, and `SSSS` is a four-digit sequence.
- The sequence is maintained independently per account type and UTC hour, starts at `0001`, and supports up to `9999` accounts per type per hour.
- Account creation requires one NRC, photo, proof-of-address, household-registration, and source-of-funds document.
- Each required document type accepts exactly one PDF, JPEG, or PNG file up to 10 MB.
- Account documents are stored privately with generated GUID filenames and begin in `PendingVerification` status.

## Users

- Every user has a `Status` (`UserStatus.Active` or `UserStatus.Disabled`); new users default to `Active`.
- A disabled user cannot log in, fetch permissions, change their password, or call any `[RequirePermission]` endpoint,
  even with an unexpired token. These requests fail with `UserAccountDisabled` (403).
- On login, the disabled check runs only after the password is verified, so account state is not revealed to callers
  who do not know the password.