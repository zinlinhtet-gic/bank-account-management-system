# Business Rules

## Account creation and numbering

- Opening balances must satisfy the selected product's minimum.
- New accounts start in `AccountStatus.Active`.
- A customer cannot hold more than one active individual account of the same product.
- If an account type configures `RequiredProductId`, at least one selected customer must own an active account of that product before the new account can be opened.
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
- The payout account must be active, individually owned by the primary holder, and not a fixed-deposit account. If omitted, the server selects the first eligible account.
- `RequiredProductId` controls whether the customer is eligible to open a product; it does not restrict the payout account.
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
- Account postings follow the account type (`AccountType`), all 422:
  - deposits need `AllowDeposit` (`DepositNotAllowed`) and do not require an existing available balance or minimum maintained balance;
  - withdrawals need `AllowWithdrawal` (`WithdrawalNotAllowed`);
  - internal, interbank and NRC transfers need `AllowTransfer` on the source (`TransferNotAllowed`);
  - withdrawals and transfers may not exceed the available balance (`InsufficientBalance`) or leave less than
    `MinimumMaintainedBalance` (`MinimumBalanceRequired`);
  - customer debits (withdrawals and the three transfer types) on the same Myanmar business day / calendar month may not exceed
    `DailyTransactionLimit` / `MonthlyTransactionLimit` (`DailyTransactionLimitExceeded` /
    `MonthlyTransactionLimitExceeded`). Refunded (cancelled or failed) transfers do not count. A null limit means no limit.

### Posting

- Every posting runs in a database transaction and row-locks the accounts it touches (`SELECT ... FOR UPDATE`, in
  ascending id order), so concurrent postings cannot overdraw an account, break a limit, or deadlock each other.
- Each posting writes one `AccountTransactions` row per customer account with the balances before and after, and
  balanced general-ledger lines in `TransactionEntries` (see the ledger table below).
- An internal transfer is balanced by two customer-deposit journal lines: a debit to the source account and a credit
  to the destination account. Interbank and NRC transfers begin with two funding lines and receive two more
  settlement/payout lines when completed.
- Before commit, the posting validator checks the expected GL account and debit/credit direction for the transaction
  type and lifecycle state, verifies every journal line equals the transaction amount, and rejects duplicate or
  missing lines.
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
- Account creation requires every individual or joint account holder to have `KycStatus.Verified`. The account-opening NRC lookup returns that status so the desktop can explain and block the action early; `AccountHolderService` validates it again on account creation and rejects pending or rejected KYC with `MessageCode.AccountHolderKycNotVerified`.

## Security (Users / Roles)

- Three roles exist: `RoleConstants.Manager` ("Branch Manager"), `RoleConstants.Officer`, `RoleConstants.Auditor`.
- In Development only, `SecuritySeeder` seeds these three roles and one example user per role (`manager1`, `officer1`, `auditor1`) with a placeholder (non-real) password hash, if the `Roles`/`Users` tables are empty. It never runs, and never overwrites existing data, outside `IsDevelopment()`.
- No authentication is implemented yet, so role-restricted endpoints (like KYC review) take the acting user's Id directly in the request body rather than reading it from an authenticated session.

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
- The monthly accounting-close occurrence is scheduled for 00:00 Asia/Rangoon on the second day of each month. It runs prior-month account maintenance, then prior-month interest accumulation, then upserts one `MonthlySummary` per GL account for the prior calendar month. Each stage uses the existing idempotent service operations; the summary is published only after both posting stages succeed.
- Saving maintenance fees accrue monthly and are deducted after calendar quarter close; dormant penalties are accrued and deducted monthly. Saving and active fixed-deposit interest accrue monthly and are credited quarterly. Monthly accruals and balance postings each create account transaction entries, balanced General Ledger entries, and audit records.
- Scheduled account operations use effective product fee and interest rules. A savings balance outside every configured interest tier earns no interest for the month (a warning is logged); other missing/invalid rules fail the affected account and are recorded by the scheduled-job retry workflow. Seeded demo saving fees are 1,000 MMK monthly and dormant penalties are 5,000 MMK monthly.
- Database lease tokens and heartbeats ensure only one application instance owns a running occurrence; if a heartbeat cannot renew the lease, the handler is cancelled and the attempt fails. Missed interval occurrences are skipped; the next interval is aligned to the UTC interval boundary. Monthly occurrences are never skipped: after a success or exhausted attempts the next run is the occurrence after the one just processed, so months missed during downtime run one by one.

## Business dates

- Timestamps are stored in UTC; every business date uses Myanmar time (Asia/Rangoon, UTC+06:30) through `Utils/BusinessTime`: posting and value dates, daily/monthly limits, fixed-deposit start dates, product effective dates, date-of-birth checks, account-number periods and scheduled-job run dates.
- A persisted `BusinessDate` is the bank-wide financial posting date and is separate from `CreatedAtUtc` and `TransactionAt`. Each transaction captures the open date when its first journal entry is posted; individual journal entries retain their own posting dates for later settlements. A closed date rejects normal posting. EOD closes the selected date and opens the next calendar date atomically.
- Operational posting and cash-session opening use the locked active business-date row. They reject rollover only when yesterday remains unclosed and today has no open business-date row; End of Day can still load and close yesterday's cash sessions by explicitly selecting that business date.
- End-of-day blocker overrides require a reason and approval from every active user with `end_of_day_approval` at request time. The requester's approval is recorded immediately; one eligible manager can authorize alone when there is only one. Required and approved user IDs, approval UTC timestamps, and the pre-close stage results are retained on the EOD run. The override skips pre-close blocker enforcement only; business dates must still close oldest-first and the GL close audit still runs.
- Reconciliation schema index and foreign-key identifiers use compact names within the configured MySQL 64-character identifier limit.
- Financial posting validates journal completeness and debit/credit equality before commit. Account reconciliation stores run/result snapshots comparing each day's last operational balance snapshot with customer-linked liability journal entries across the inclusive selected date range. It selects snapshots by posting date and then posting sequence, records and updates exceptions without changing account balances, and resolves the date's exception with history after a matched rerun.
- First detection of an account, ledger, or physical-cash discrepancy appends an initial `None` → `Open` event to the exception timeline. Subsequent investigation, recount, correction-reference, and resolution events are appended; prior history is preserved.
- Teller/vault cash positions record opening cash, posted cash movements, transfers, and physical counts by teller/vault and business date; neither positions nor reconciliation exceptions are branch-scoped. Count differences create investigation exceptions and cannot overwrite expected cash. Corrective changes must go through transaction posting and a subsequent reconciliation/count. NRC pickup branch remains a separate transaction delivery location.
- A teller may have one open teller session per business date; different tellers may have concurrent open sessions. One vault session may be open per date. Closed sessions remain in history and do not block another session for the same teller/date. Session creation remains protected by the active business-date lock. A withdrawal or approved negative cash adjustment is rejected with `InsufficientCashPositionBalance` if it would make expected cash negative.
- A teller may inspect only their own cash session; a user with `end_of_day_approval` can inspect teller and vault session histories for close review. Session detail exposes immutable movement/count rows and linked transaction references.
- Every officer write API requires that officer's open teller session for the active posting business date. Session reads remain available, and opening the officer's teller session is the only write exempt from this gate. Missing sessions return `CashSessionNotOpen`; manager and other role authorization is unchanged.
- Cash adjustments require a posted transaction with a non-zero Cash on Hand journal effect, a signed request amount exactly equal to that effect, and approval by a distinct user with `end_of_day_approval`. Only approval applies the effect to expected cash; requester and approver are retained on the movement.
- Reconciliation correction requests are raised from the selected exception after reviewing posted transactions affecting its account and their journal lines. A distinct user with `transaction_correction_approval` may approve or reject with a required note. Approval posts a full inverse of the source transaction's account and GL entries on the current open business date, links it through `ReversalOfTransactionId`, and marks the source transaction reversed; it never reopens a closed date. A debit that would make an account balance negative is rejected. Settled NRC/interbank transfers require an external recovery reference and reviewer attestation. Any replacement is posted separately through the normal transaction flow. A later reconciliation run must match on or after the reversal date before the originating exception can resolve.
- Cash adjustment requests and decisions are available on the Cash Adjustments tab. Approval and rejection require a distinct EOD approver; rejection requires a reason and does not modify the expected cash position.
- Cash-operation inputs reject invalid IDs, negative opening/count amounts, nonpositive transfers, zero adjustments, and amounts with more than two decimal places. Transfer and adjustment notes are limited to 500 characters; count notes are limited to 2,000. Request validation errors return HTTP 400.
- Closing an open session with positive physical cash atomically records a `CashHandoff` linked to its count. An active user with cash-operations, audit, or EOD-approval permission must be selected; the sender cannot receive their own cash. A zero count does not require or create a handoff. The recipient alone may accept or decline; decline requires a note and continues to block EOD. The sender or an audit/EOD reviewer can reassign a non-accepted handoff, which returns it to `PendingAcceptance`. Append-only handoff history records creation, acknowledgement, decline and reassignment with actor, recipient transition, note and UTC timestamp.
- Cash session opening and physical count-close use `Idempotency-Key` (64 characters maximum); the caller reuses the same key on retries and must use a new key for changed input. Key uniqueness is scoped to the initiating user and stored durably with the session/count. Cash sessions and handoffs carry optimistic `Version` tokens. Count and handoff mutation requests must submit the version they reviewed; stale versions fail with `ConcurrentModification`. Repeated accept/decline in the already-applied state and repeated same-recipient reassignment return the existing state without adding duplicate history.
- EOD treats both open cash sessions and every handoff not in `Accepted` state (including declined handoffs) as blocking cash-stage issues. Reconciliation and EOD do not overwrite expected cash with a physical count.
- Account reconciliation accepts an inclusive range of at most 366 business dates. Exception status filters accept only `Open`, `UnderInvestigation`, `AdjustmentRequired`, or `Resolved`; status updates must follow allowed investigation transitions. Invalid ranges/statuses/transition requests return HTTP 400. Reconciliation does not update `Account.Balance`.
- `ScheduledAccountReconciliationService` invokes the same reconciliation service used by the API under the existing scheduled-job lease/retry mechanism and the disabled system actor. `AccountingReconciliation:IntervalMinutes` configures the interval (default 1440; `0` disables it).
- EOD blocks on pending/failed transactions, missing or unbalanced journals, open cash sessions, unresolved critical exceptions, or unmatched account balances. Pre-close runs account reconciliation; close reruns it once while holding the BusinessDate lock so postings cannot race final checks. Approval requires the run's business date to remain open and a reviewer distinct from the preparer. No automatic EOD time is hard-coded.
- End-of-day for a date covers transactions made during that Myanmar day plus transactions whose ledger lines carry that posting date. Every entry of those transactions is checked for completeness and balance; only lines posted on the audit date enter the day's totals, reconciliation and daily summaries. Accrual transactions (`Accrued`) are included.

## Monthly summary source selection and failed-job recovery

- A monthly close is blocked until the previous month's final business date is closed through End of Day. The manager alert reports that reason and opens the same EOD flow. Managers may request a retry of that exact scheduled occurrence after closing the date; the retry request and each execution attempt remain audited. Only failures that exhausted automatic retries are listed.
- Managers with `scheduled_job_management` can view exhausted scheduled-job failures and request a same-occurrence retry. A successful retry resolves the alert. A retry that exhausts its attempts returns the failure to the alert list; it never closes a business date on the manager's behalf.
- Monthly close reads all `TransactionEntries` posted in the target calendar month to calculate its debit and credit totals, including late scheduled accrual entries posted to that month.
- Opening balance uses the latest earlier `MonthlySummary`, then the latest `DailySummary` after that monthly snapshot where one exists. If neither snapshot exists for a GL account, opening balance is derived from raw journal entries before the target month; missing history is not silently treated as zero.
- Accounting entry detail, monthly close calculation, and reconciliation use accounting rows in their defined GL scope. Daily and monthly summary tables serve prior-period opening/closing snapshots and summary reporting, not as substitutes for journal detail.
