# Business Rules

## Accounts

- Account names are required and capped by `AccountConstants.AccountNameMaximumLength`.
- Opening balances cannot be below `AccountConstants.MinimumOpeningBalance`.
- Account types must be valid `AccountType` enum values.
- New accounts are created with `AccountStatus.Active`.
- Account numbers are generated server-side using `AccountConstants.AccountNumberPrefix`.

## Transactions (`api/transactions`)

### Permissions

| Endpoints | Permission |
| --- | --- |
| `GET` list, `GET {id}`, `GET accounts/{accountId}/statement` | `transactions` (officers) or `transaction_history` (auditors) |
| `GET other-banks`, `GET branches` (pickers for the transfer forms) | `transactions` |
| `deposit`, `withdrawal`, `transfer/internal`, `transfer/interbank`, `transfer/nrc`, `transfer/nrc/{id}/cancel`, `transfer/nrc/{id}/paid-out`, `nrc-pickup` | `transactions` |
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
  accounts are allowed. Refunds only need the account to be not closed.
- Debits follow the account type (`AccountType`), all 422:
  - withdrawals need `AllowWithdrawal` (`WithdrawalNotAllowed`);
  - internal, interbank and NRC transfers need `AllowTransfer` on the source (`TransferNotAllowed`);
  - the debit may not exceed the available balance (`InsufficientBalance`) or leave less than
    `MinimumMaintainedBalance` (`MinimumBalanceRequired`);
  - customer debits (withdrawals and the three transfer types) on the same UTC day / calendar month may not exceed
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
  requests arrive at the same time. A key already used by another user, transaction type or amount is
  `IdempotencyKeyReused` (409).
- An NRC transfer replay does not contain the pickup code: only its hash is stored.

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
  - At our branch, pickup (`nrc-pickup`) verifies the code and pays out in cash, or into `destinationAccountId` when
    the receiver has an account. A wrong code is `InvalidPickupCode` (400) and is counted; after
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
hit Customer Deposits (2000) with the customer account id.

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
| NRC pickup at our branch, into an account | NRC Transfers Payable | Customer Deposits (receiver) |
| NRC paid out by another bank | NRC Transfers Payable | Due from Other Banks |
| NRC cancelled (refund) | NRC Transfers Payable | Cash on Hand, or Customer Deposits (sender) |

## Users

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
