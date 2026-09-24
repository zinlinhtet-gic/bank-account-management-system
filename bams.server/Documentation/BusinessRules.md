# Business Rules

## Account creation and numbering

- Opening balances must satisfy the selected product's minimum.
- New accounts start in `AccountStatus.Active`.
- A customer cannot hold more than one active individual account of the same product.
- Account numbers are generated server-side as 16 digits in `TTyyyyMMddHHSSSS` format.
- `TT` is the two-digit account-type ID, `yyyyMMddHH` is the UTC generation hour, and `SSSS` is a per-product, per-hour sequence from `0001` through `9999`.

## Holders

- Individual accounts require one registered customer and store 100% ownership.
- Joint accounts require two distinct registered customers and exactly one primary holder.
- Joint ownership percentages must each be greater than zero, no greater than 100, and total exactly 100.
- Joint-holder updates provide the complete desired percentages, primary designation, and shared signing rule.
- Existing holder updates cannot add, remove, or replace customers and are forbidden after account closure.

## Status lifecycle

- Account names are required and capped by `AccountConstants.AccountNameMaximumLength`.
- Opening balances cannot be below `AccountConstants.MinimumOpeningBalance`.
- Account types must be valid `AccountType` enum values.
- Fixed-deposit classification uses the persisted `AccountType.IsFixedDeposit` flag rather than product-name or category parsing.
- Normal Deposit, Special Deposit, and Hundred-Days Deposit are fixed-deposit account types.
- Every fixed-deposit account type requires an individual Normal Saving account for payout.
- Fixed-deposit creation requires an applicable interest-rate rule, renewal instruction, and calculation-source flag.
- The fixed-deposit calculation-source flag is immutable after creation.
- A supplied or inferred payout account must be the primary holder's active individual Normal Saving account.
- Fixed-deposit principal cannot be negative. Closed and Cancelled deposits are terminal.
- The fixed-deposit API may update only the payout account and renewal instruction; current-principal and status changes are internal service operations.
- Internal workflows may update current principal, status, or both atomically. A combined maturity update applies the new principal before creating a renewed term.
- Maturing a renewable deposit creates a successor term using the same rate, term, payout account, and renewal configuration.
- Renewal uses CurrentPrincipal when `CalculateFromCurrent` is true and OriginalPrincipal otherwise.
- Account opening, account updates, holder updates, and fixed-deposit writes create transactional audit records.
- Accounts, account holders, and fixed deposits use application-managed optimistic concurrency versions. Every successful modification increments the affected record's version.
- Mutation callers must supply the version they last read. Stale versions reject the entire operation without partial business or audit writes.
- New accounts are created with `AccountStatus.Active`.
- Account numbers are generated server-side using `AccountConstants.AccountNumberPrefix`.
