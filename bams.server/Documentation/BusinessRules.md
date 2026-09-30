# Business Rules

## Account creation and numbering

- Opening balances must satisfy the selected product's minimum.
- New accounts start in `AccountStatus.Active`.
- A customer cannot hold more than one active individual account of the same product.
- Account types may allow citizens, foreigners, or both; account creation rejects any holder whose `CustomerType` is disallowed.
- `CitizenRequiredRefer` and `ForeignRequiredRefer` are nonnegative minimum counts. Joint accounts require the sum of each holder's applicable minimum.
- Each supplied referrer NRC must resolve to a distinct existing customer with at least one account-holder relationship. Referrer links are stored against the new account in the account-creation transaction.
- Account numbers are generated server-side as 16 digits in `TTyyyyMMddHHSSSS` format.
- `TT` is the two-digit account-type ID, `yyyyMMddHH` is the Myanmar local (Asia/Rangoon) generation hour, and `SSSS` is a per-product, per-hour sequence from `0001` through `9999`.

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

- Product category is stored as `AccountTypeCategory`; fixed-deposit behavior uses the `FIXED` category.
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

## Transactions (`api/transactions`)

### Permissions

| Endpoints | Permission |
| --- | --- |
| `GET` list, `GET {id}`, `GET accounts/{accountId}/statement` | `transactions` (officers) or `transaction_history` (auditors) |
| `GET other-banks`, `GET branches` (pickers for the transfer forms) | `transactions` |
| `deposit`, `withdrawal`, `transfer/internal`, `transfer/interbank`, `transfer/nrc`, `transfer/nrc/{id}/cancel`, `transfer/nrc/{id}/reissue-code`, `transfer/nrc/{id}/paid-out`, `nrc-pickup` | `transactions` |
| `transfer/interbank/{id}/complete`, `transfer/interbank/{id}/fail` | `transactions` or `operation` (managers) |

### Validation

- Amounts must be greater than zero with at most `TransactionConstants.MaximumAmountDecimalPlaces` (2) decimal places
  (`InvalidAmount`). Text fields are capped by the `TransactionConstants.*MaximumLength` values (`FieldTooLong`).
- Transfers need two different accounts (`SameSourceAndDestinationAccount`).
- Lists and statements: `page` starts at 1, `pageSize` is 1-100 (default 20), otherwise `InvalidRequest`;
  `from` must be before `before` (`InvalidDateRange`). The list filters by `accountId` or by exact `accountNo`
  (auditors cannot list accounts, so they filter by number), `type`, `status` and the date range.

### Account rules

- Closed, frozen and suspended accounts can neither send nor receive funds (`AccountNotOperational`, 422). Dormant
  accounts are allowed, and any teller posting to a dormant account (deposit, withdrawal, transfer, NRC funding,
  refund) moves it back to `Active` with a status-history row (`AccountConstants.DormantReactivationReason`,
  changed by the posting user). Scheduled interest, fee and dormant-penalty postings do not reactivate an account.
  Refunds only need the account to be not closed.
- Debits follow the account type (`AccountType`), all 422:
  - deposits need `AllowDeposit` (`DepositNotAllowed`);
  - withdrawals need `AllowWithdrawal` (`WithdrawalNotAllowed`);
  - internal, interbank and NRC transfers need `AllowTransfer` on the source (`TransferNotAllowed`);
  - the debit may not exceed the available balance (`InsufficientBalance`) or leave less than
    `MinimumMaintainedBalance` (`MinimumBalanceRequired`);
  - customer debits (withdrawals and the three transfer types) on the same Myanmar business day / calendar month may not exceed
    `DailyTransactionLimit` / `MonthlyTransactionLimit` (`DailyTransactionLimitExceeded` /
    `MonthlyTransactionLimitExceeded`). Refunded (cancelled or failed) transfers do not count. A null limit means no limit.

### Posting

- Every posting runs in a database transaction and row-locks the accounts it touches (`SELECT ... FOR UPDATE`, in
  ascending id order), so concurrent postings cannot overdraw an account, break a limit, or deadlock each other.
- Each posting writes one `AccountTransactions` row per customer account with the balances before and after, and
  balanced general-ledger lines in `TransactionEntries` (see the ledger table below).
- Every posting, pickup (including failed attempts), cancellation and settlement writes an `AuditLogs` row:
  `Action` from `AuditConstants`, `EntityId` = transaction number, `NewValues` = JSON details, plus the user, IP and
  User-Agent. Pickup codes are never logged.

### Idempotency

- Posting endpoints accept an optional `Idempotency-Key` header (max 64 characters). The desktop client should send a
  new key per user action and reuse it on retries.
- A repeated key returns the original transaction with the normal success code and posts nothing, also when both
  requests arrive at the same time. A key already used by another user, transaction type, amount or set of
  customer accounts is `IdempotencyKeyReused` (409).
- An NRC transfer replay does not contain the pickup code: only its hash is stored. If the code was lost, reissue it
  (below).

### Transfer life cycles

- Deposits, withdrawals and internal transfers are `Completed` immediately (`TransactionCompletedSuccessfully`).
- Interbank transfers debit the source and stay `Pending` (`GatewayStatus.Pending`) until the gateway result is
  recorded (`TransactionSubmittedSuccessfully`). The destination bank must exist in `OtherBanks` (`OtherBankNotFound`).
  - `complete` marks it `Completed` / `GatewayStatus.Success` (`InterbankTransferSettledSuccessfully`).
  - `fail` marks it `Failed` / `GatewayStatus.Failed` and refunds the sender (`TransactionRefundedSuccessfully`).
  - Either one on a transfer that is no longer pending is `TransactionNotPending` (422).
- NRC transfers are remittances: a sender at the counter pays for a named receiver, identified by NRC, to collect
  the money at one of our branches or at another bank. Neither needs an account with us.
  - The sender pays in cash (`sourceAccountId` null) or from their account (debit rules apply).
  - `deliveryType` is `Branch` with `pickupBranchId` (an active branch, else `BranchNotFound`) or `OtherBank` with
    `pickupOtherBankId` (`OtherBankNotFound`); anything else is `InvalidRequest`.
  - The transfer stays `Pending`. The response contains a six-digit pickup code, shown only once; only its hash is
    stored. It is valid for `TransactionConstants.NrcPickupCodeValidity` (24 hours).
  - `transfer/nrc/{id}/reissue-code` replaces the code of a pending transfer (a lost code, or a create response
    that never arrived): the old code stops working immediately, failed attempts reset to zero and the new code is
    valid for a fresh 24 hours. The new code is returned once in `pickupCode` (`NrcPickupCodeReissuedSuccessfully`)
    and is not logged; a transfer that is no longer pending is `TransactionNotPendingPickup` (422).
  - At our branch, pickup (`nrc-pickup`) pays out in cash only, since the receiver need not have an account. The
    officer enters `receiverName` and `receiverNrc` from the collector's NRC card; they must match the receiver the
    sender designated (ignoring case and extra whitespace), else `NrcPickupReceiverMismatch` (422). Then the code is
    verified. A mismatch counts as a failed attempt, like a wrong code. A wrong code is `InvalidPickupCode` (400) and is counted; after
    `TransactionConstants.MaximumFailedPickupAttempts` (5) every pickup is `PickupAttemptsExceeded` (422). An expired
    code is `PickupCodeExpired` (422).
  - At another bank, that bank checks the code and pays; an officer then records it with
    `transfer/nrc/{id}/paid-out`, which completes the transfer.
  - Pickup on an other-bank transfer, or `paid-out` on a branch transfer, is `NrcPickupLocationMismatch` (422).
  - Cancel: a pending transfer (expired, blocked, or at the sender's request) can be cancelled; it becomes
    `Cancelled` and the sender is refunded the way they paid, in cash or to their account (`TransactionRefundedSuccessfully`).
  - Pickup, payout or cancel of a transfer that is no longer pending is `TransactionNotPendingPickup` (422). Each of
    them deletes the code hash.
- Refunds are new `Reversal` transactions (`ReversalOfTransactionId` = the original) that credit the account the
  original debited, or pay cash back when the sender paid cash. `cancel` and `fail` return the refund transaction.

### General ledger

GL accounts are created at startup by `ChartOfAccountsSeeder` (codes in `AccountingConstants`). Customer entries always
hit Customer Deposits (2000) with the customer account id, including scheduled interest credits, maintenance fees
and dormant penalties. The scheduled-operation accounts (1101, 1102, 2101, 4001, 4002, 6001) are seeded by
`ProductSeeder`.

| Event | Debit | Credit |
| --- | --- | --- |
| Deposit | Cash on Hand (1000) | Customer Deposits (2000) |
| Withdrawal | Customer Deposits | Cash on Hand |
| Internal transfer | Customer Deposits (source) | Customer Deposits (destination) |
| Interbank transfer | Customer Deposits | Interbank Clearing (2100) |
| Interbank settled | Interbank Clearing | Due from Other Banks (1100) |
| Interbank failed (refund) | Interbank Clearing | Customer Deposits |
| NRC transfer, paid in cash | Cash on Hand | NRC Transfers Payable (2200) |
| NRC transfer, paid from account | Customer Deposits | NRC Transfers Payable |
| NRC pickup at our branch, in cash | NRC Transfers Payable | Cash on Hand |
| NRC paid out by another bank | NRC Transfers Payable | Due from Other Banks |
| NRC cancelled (refund) | NRC Transfers Payable | Cash on Hand, or Customer Deposits (sender) |

## Users

- New users default to `UserStatus.Active`.
- Disabled users cannot sign in, fetch permissions, change passwords, or call `[RequirePermission]` endpoints, even with an unexpired token.
- Login verifies the password before reporting disabled status so callers cannot discover account state without valid credentials.
- Every user has a `Status`: `Active`, `Disabled` or `Deleted`; new users default to `Active`.
- A disabled or deleted user cannot log in, fetch permissions, change their password, or call any `[RequirePermission]`
  endpoint, even with an unexpired token. These requests fail with `UserAccountDisabled` or `UserAccountDeleted` (403).
  The check lives in one place: `UserStatusExtensions.EnsureCanSignIn()`.
- On login, the status check runs only after the password is verified, so account state is not revealed to callers
  who do not know the password.
- Passwords are hashed only through `Utils/Security/PasswordHasher` (login, change password, user management, seeding).
- Change password: the new password must meet the complexity rules, must not be any role default password in
  `UserConstants.DefaultPasswordsByRole` (`DefaultPasswordNotAllowed`; managers know these), and must differ from the
  current password (`NewPasswordSameAsCurrent`). The default check runs first, so retyping the default gets that message.

## User Management (`api/users`, permission `user_management`: managers)

- Managers can list, view, create, edit, reset the password of, and delete manager, officer and auditor accounts.
- The list never includes deleted users and is sorted by username. Filters: search (part of username, full name or
  email), role, and a created-date range (`createdFrom` inclusive, `createdBefore` exclusive; `from >= before` is
  `InvalidDateRange`).
- Create / update validation (`UserConstants`): full name, username, email and role are required; phone is optional.
  Full name ≤ 150 characters (the desktop form merges first and last name), username matches
  `UserConstants.UsernamePattern`, email matches `EmailPattern`, phone matches `PhonePattern`. Only roles listed in
  `UserConstants.DefaultPasswordsByRole` can be assigned (`InvalidRole` otherwise).
- Usernames and emails are unique across **all** users, including deleted ones, so a deleted user's username stays
  reserved (`UsernameAlreadyExists` / `EmailAlreadyExists`).
- New users and password resets get the role's default password (`Manager123!`, `Officer123!`, `Auditor123!`) with
  `MustChangePassword = true`, so the user must choose a new password at the next login.
- Delete is a soft delete: `Status = Deleted`, the row is kept. A deleted user who logs in with the correct password
  gets `UserAccountDeleted`; any token they still hold stops working at once.
- Online presence (the list's Status column): a user is **online** when `OnlineStatus = Active` and `LastSeenAt` is
  within `UserConstants.OnlinePresenceTimeout` (45 seconds). Login sets both; the desktop calls
  `POST api/auth/heartbeat` every 15 seconds while signed in (the open list refreshes every 10 seconds); `POST api/auth/logout` sets `OnlineStatus = Inactive`.
  An app closed without logging out drops to offline once its heartbeats stop. The rule lives only in
  `UserMappings.IsOnline`.
- Self-delete is allowed, but the bank must keep one active manager: deleting the only active manager, or changing
  that manager's role, fails with `LastManagerCannotBeRemoved` (422).

## Generic scheduled jobs

- Jobs are registered in code by stable key and a reusable interval or monthly schedule, then synchronized to `ScheduledJobs` at startup.
- Each attempt is recorded in `ScheduledJobExecutions`; failures include bounded exception details. Jobs retry up to `Jobs:MaximumAttempts`, then retain `Failed` as their latest status and proceed to the next recurrence.
- Account maintenance and interest accumulation run at 00:00 Asia/Rangoon on the fifth day of each month. They process accounts in batches and persist monthly accruals idempotently.
- Saving maintenance fees accrue monthly and are deducted after calendar quarter close; dormant penalties are accrued and deducted monthly. Saving and active fixed-deposit interest accrue monthly and are credited quarterly. Monthly accruals and balance postings each create account transaction entries, balanced General Ledger entries, and audit records.
- Scheduled account operations use effective product fee and interest rules. A savings balance outside every configured interest tier earns no interest for the month (a warning is logged); other missing/invalid rules fail the affected account and are recorded by the scheduled-job retry workflow. Seeded demo saving fees are 1,000 MMK monthly and dormant penalties are 5,000 MMK monthly.
- Database lease tokens and heartbeats ensure only one application instance owns a running occurrence; if a heartbeat cannot renew the lease, the handler is cancelled and the attempt fails. Missed interval occurrences are skipped; the next interval is aligned to the UTC interval boundary. Monthly occurrences are never skipped: after a success or exhausted attempts the next run is the occurrence after the one just processed, so months missed during downtime run one by one.

## Business dates

- Timestamps are stored in UTC; every business date uses Myanmar time (Asia/Rangoon, UTC+06:30) through `Utils/BusinessTime`: posting and value dates, daily/monthly limits, fixed-deposit start dates, product effective dates, date-of-birth checks, account-number periods and scheduled-job run dates.
- End-of-day for a date covers transactions made during that Myanmar day plus transactions whose ledger lines carry that posting date. Every entry of those transactions is checked for completeness and balance; only lines posted on the audit date enter the day's totals, reconciliation and daily summaries. Accrual transactions (`Accrued`) are included.
