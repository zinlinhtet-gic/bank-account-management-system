# Business Rules

## Accounts

- Account names are required and capped by `AccountConstants.AccountNameMaximumLength`.
- Opening balances cannot be below `AccountConstants.MinimumOpeningBalance`.
- Account types must be valid `AccountType` enum values.
- New accounts are created with `AccountStatus.Active`.
- Account numbers are generated server-side using `AccountConstants.AccountNumberPrefix`.
