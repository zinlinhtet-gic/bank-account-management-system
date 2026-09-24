# Business Rules

## Accounts

- Account names are required and capped by `AccountConstants.AccountNameMaximumLength`.
- Opening balances cannot be below `AccountConstants.MinimumOpeningBalance`.
- Account types must be valid `AccountType` enum values.
- New accounts are created with `AccountStatus.Active`.
- Account numbers are generated server-side using `AccountConstants.AccountNumberPrefix`.

## Users

- Every user has a `Status` (`UserStatus.Active` or `UserStatus.Disabled`); new users default to `Active`.
- A disabled user cannot log in, fetch permissions, change their password, or call any `[RequirePermission]` endpoint,
  even with an unexpired token. These requests fail with `UserAccountDisabled` (403).
- On login, the disabled check runs only after the password is verified, so account state is not revealed to callers
  who do not know the password.
