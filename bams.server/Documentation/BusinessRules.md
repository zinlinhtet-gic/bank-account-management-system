# Business Rules

## Accounts

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
- Active accounts may become Dormant, Suspended, Frozen, or Closed.
- Dormant accounts may become Active, Suspended, or Frozen.
- Suspended and Frozen accounts may become Active. Closed accounts are terminal.
- Every status change records the old status, new status, reason, user, and UTC change time in `AccountStatusHistory`.
- Accounts retain the latest UTC time at which they entered each supported status.
- A customer cannot hold more than one active individual account of the same account type.
- Individual accounts require one registered customer NRC.
- Shared accounts require two registered customer NRCs and create joint ownership records for both customers.
- Individual account holders are stored with 100 percent ownership.
- Joint holder percentages must each be greater than zero, no greater than 100, and total exactly 100 percent.
- Joint accounts have exactly two distinct holders and exactly one primary holder.
- Existing holder details may be updated only for non-closed joint accounts; customers cannot be added, removed, or replaced by this operation.
- A holder update supplies the complete desired primary designation, ownership percentages, and one shared signing rule for both holders.
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
