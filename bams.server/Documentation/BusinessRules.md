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

- Active accounts may become Dormant, Suspended, Frozen, or Closed.
- Dormant accounts may become Active, Suspended, or Frozen.
- Suspended and Frozen accounts may become Active.
- Closed accounts are terminal.
- Every status change stores the old and new status, optional reason, acting user, and UTC change time.
- Accounts retain the latest UTC timestamp at which they entered each supported status.

## Fixed deposits

- Fixed-deposit classification uses persisted `AccountType.IsFixedDeposit`, not product-name parsing.
- Normal Deposit, Special Deposit, and Hundred-Days Deposit are fixed-deposit products.
- Creation requires an applicable interest-rate rule, renewal instruction, and calculation-source flag.
- The calculation-source flag is immutable after creation.
- Each fixed-deposit product requires an eligible individual payout account owned by the primary holder.
- Current principal cannot be negative; Closed and Cancelled deposits are terminal.
- The HTTP update can change only payout account and renewal instruction.
- Internal workflows may update current principal, status, or both atomically.
- Maturing a renewable deposit creates a successor using the same rate, term, payout account, and renewal settings.
- Renewal uses `CurrentPrincipal` when `CalculateFromCurrent` is true and `OriginalPrincipal` otherwise.

## Documents

- Account creation requires NRC, photo, proof of address, household registration, and source-of-funds documents for seeded products.
- Each document type may appear once and accepts PDF, JPEG, or PNG up to 10 MB.
- Files use generated private references and begin in `PendingVerification` status.

## Concurrency and auditing

- Account, account-holder, and fixed-deposit mutations create audit records within the owning transaction.
- These entities use application-managed optimistic concurrency versions.
- Successful modifications increment the affected entity version.
- Callers must submit the version they last read; stale versions reject the complete operation without partial business or audit writes.

## Users

- New users default to `UserStatus.Active`.
- Disabled users cannot sign in, fetch permissions, change passwords, or call `[RequirePermission]` endpoints, even with an unexpired token.
- Login verifies the password before reporting disabled status so callers cannot discover account state without valid credentials.
