# API Contracts

This page documents selected routes and behavior for accounts, customers, account products, operations, accounting, cash operations, and business dates. It is not a complete inventory of every controller. For a complete current route list, inspect `Controllers/` and each controller's authorization attributes.

The [OpenAPI file](AccountsApi.openapi.yaml) is a machine-readable contract with the coverage stated in its `info` description. Do not assume it covers routes that are absent from its `paths` section. `Accounts.http` contains runnable account examples; `transactions.http` contains transaction examples.

## Endpoint overview

Successful API operations generally return `ApiMessageResponse<T>` with the payload in `data`; verify endpoints with special responses such as authentication or file delivery in their controller. Expected errors use `ApiErrorResponse` with a stable code and trace ID. Clients should branch on the numeric code rather than message text. The desktop `ApiClient` handles the common envelope.

| Method | Route | Authentication | Purpose |
| --- | --- | --- | --- |
| GET | `/api/account-types` | Public | List active account products. |
| GET | `/api/interest-rate-rules?accountTypeId={id}` | `account_management` | List active interest rules currently effective for one account type. |
| GET | `/api/accounts` | `account_management` | List accounts with cursor pagination. |
| GET | `/api/accounts/{id}` | `account_management` | Get one account. |
| GET | `/api/accounts/customer-lookup?nrc={nrc}` | `account_management` | Find an existing customer by NRC for account opening. |
| POST | `/api/customers` | `customer_management` | Persist a customer profile and return the saved profile. |
| GET | `/api/accounts/opening-options?holderNrc={nrc}` | `account_management` | Get eligible products, required documents, and individual accounts matching their configured payout products. |
| GET | `/api/accounts/{id}/transactions` | `account_management` | Get account transaction entries. |
| GET | `/api/accounting/reconciliation/exceptions/{id}/correction-candidates` | `reconciliation_investigation` | List posted account transactions and journal previews for exception review. |
| GET | `/api/accounting/reconciliation/account-options?search={term}` | `accounting` | Search up to 50 accounts by full or partial account number for the reconciliation scope selector. |
| GET | `/api/accounting/reconciliation/staff-options` | `reconciliation_investigation` | List staff names, usernames, and roles for assignment without exposing numeric IDs in the UI. |
| POST | `/api/accounting/reconciliation/exceptions/{id}/correction-requests` | `reconciliation_investigation` | Request a reviewed full reversal of a posted account transaction. |
| POST | `/api/accounting/reconciliation/exceptions/{id}/correction-review` | `transaction_correction_approval` | Approve and post or reject a correction request. |
| POST | `/api/cash-operations/adjustments/{id}/reject` | `end_of_day_approval` | Reject a pending cash adjustment with a required reason. |
| GET | `/api/accounts/{id}/status-history` | `account_management` | Get account status changes. |
| GET | `/api/accounts/{id}/interest-accruals` | `account_management` | Get calculated interest accrual periods. |
| POST | `/api/accounts` | `account_management` | Create an account. |
| PATCH | `/api/accounts/{id}/freeze` | `account_management` | Freeze an Active or Dormant account. |
| PATCH | `/api/accounts/{id}/suspend` | `account_management` | Suspend an Active or Dormant account. |
| PATCH | `/api/accounts/{id}/reactivate` | `account_management` | Reactivate a Dormant, Suspended, or Frozen account. |
| PUT | `/api/accounts/{id}/holders` | `account_management` | Update both holders of a joint account. |
| PATCH | `/api/accounts/fixed-deposits/{fixedDepositId}` | `account_management` | Update payout and renewal instructions. |
| POST | `/api/accounting/reconciliation/accounts` | `accounting` | Compare operational balances to customer liability ledger balances. |
| GET | `/api/accounting/reconciliation/exceptions` | `accounting` | List reconciliation exceptions with date/status filters and paging. |
| GET | `/api/accounting/reconciliation/exceptions/{id}` | `accounting` | Get an exception and its append-only status/notes audit timeline. |
| PATCH | `/api/accounting/reconciliation/exceptions/{id}` | `accounting` and `reconciliation_investigation` | Update assignment, investigation status, notes, and correction reference; cannot resolve directly. |
| GET | `/api/cash-operations/sessions` | `cash_operations`, `audit`, or `end_of_day_approval` | List teller/vault sessions for a business date; auditors and EOD approvers can review all positions. |
| GET | `/api/cash-operations/sessions/{id}` | `cash_operations`, `audit`, or `end_of_day_approval` | Read an authorized cash session with its movement and physical-count history and linked transaction references. |
| POST | `/api/cash-operations/sessions` | `cash_operations` | Open a teller or vault cash position. |
| POST | `/api/cash-operations/sessions/{id}/transfers` | `cash_operations` | Transfer expected cash between positions on the same business date. Cash positions are not branch-scoped. |
| POST | `/api/cash-operations/sessions/{id}/count` | `cash_operations` | Record physical cash count and create a mismatch exception. |
| POST | `/api/cash-operations/sessions/{id}/adjustments` | `cash_operations` | Submit a signed adjustment linked to the exact posted Cash on Hand journal effect. |
| GET | `/api/cash-operations/adjustments` | `cash_operations`, `end_of_day_approval`, or `audit` | List cash adjustment requests for review. |
| POST | `/api/cash-operations/adjustments/{id}/approve` | `end_of_day_approval` | Approve another user's valid adjustment and apply it to the expected position. |
| GET | `/api/operations/business-date` | `accounting`, `audit`, `cash_operations`, or `end_of_day_approval` | Read the current or next-to-open persisted business date; the read does not create it. |
| POST | `/api/operations/business-date/start` | `end_of_day_approval` | Requires `startingCash`; atomically opens today, its vault and optional session schedules, deactivating prior dates. |
| GET | `/api/operations/business-date/dates` | `accounting`, `audit`, or `end_of_day_approval` | Search persisted business dates by optional date range. |
| GET | `/api/operations/business-date/dates/{date}/latest-run` | `accounting`, `audit`, or `end_of_day_approval` | Load the latest EOD run and any pending manager override approvals. |
| POST | `/api/operations/business-date/{date}/pre-close` | `accounting` or `audit` | Run and persist EOD pre-close stages. |
| POST | `/api/operations/business-date/runs/{id}/approve` | `end_of_day_approval` | Approve a run as a different user than the preparer. |
| POST | `/api/operations/business-date/runs/{id}/close` | `end_of_day_approval` | Close after locking and rechecking blockers; the next date is opened separately by a manager. |
| POST | `/api/operations/business-date/{date}/force-close-request` | `end_of_day_approval` | Request a blocker override with a reason; the requester's approval is recorded. |
| POST | `/api/operations/business-date/runs/{id}/force-close-approve` | `end_of_day_approval` | Record this manager's approval; close is enabled after every active manager approves. |

## Available account types

`GET /api/account-types` returns active products ordered by ID. Each `AccountTypeResponse` includes opening and maintained balances, transaction limits and capabilities, required-product information, fixed-deposit classification, allowed customer types, and customer-type-specific referrer minimums.

`GET /api/accounts/customer-lookup?nrc={nrc}` returns the matching customer, including `KycStatus`. Every holder must be `Verified` before an account can be created; the server enforces this rule even when the account API is called directly. `GET /api/accounts/opening-options` filters products by all selected holders' customer types and account-type eligibility, applies each product's `RequiredProductId` rule to customer eligibility (an active account of that type must be owned by at least one selected customer), excludes account types already actively held by the selected customer or by both selected customers in a shared application, includes product document requirements, and returns the primary holder's active individual non-fixed-deposit accounts as payout choices. The desktop uses those payout choices directly; the server independently validates the payout owner, ownership type, active status, and non-fixed-deposit account type when creating the account. Required products and payout accounts are separate rules. `POST /api/customers` is the Customer Management creation endpoint; it accepts multipart form data and returns a full `CustomerResponse`. New customers need a verified KYC review before account opening.

Account transaction, status-history, and interest-accrual routes return the records for one existing account ordered newest first.

Transaction summaries, detail, and posting responses include `businessDate` separately from the UTC `transactionAt` timestamp.

Cash funded deposit, withdrawal, NRC transfer, NRC pickup, and cash refund requests are associated server-side with the actor's activated teller session for the active posting business date. Transaction request DTOs do not require or accept a cash-session identifier; the physical movement is saved atomically with posting. If no active teller session exists, the server returns `CashSessionNotOpen`. Managers create assigned teller sessions with required Myanmar local start and end times; `POST /api/cash-operations/sessions/{id}/activate` activates an assigned session and receives its reserved starting cash atomically, only within that window. A teller session still scheduled after its end time is automatically closed; activated sessions are not auto-closed by this expiry. Cash proposals reserve available vault cash at manager approval and move balances only after officer receipt; officer rejection releases the reservation. Vault decreases cannot exceed the balance remaining after other reservations. A teller close may route counted cash to the vault and credit it after manager acknowledgement. A physical count is immutable and never overwrites expected cash. Cash adjustments require a signed amount equal to a posted transaction's Cash on Hand journal effect and independent approval. Account reconciliation preserves run/result snapshots and exception history; mismatches are resolved only after a matched rerun. Scheduled reconciliation uses its scheduled occurrence's Myanmar business date and does not require an open business date or cash session. Scheduled financial postings likewise use their explicit accounting period dates. EOD initiation is manual.

Officer writes to `/api/transactions`, `/api/cash-operations`, and `/api/cash-handoffs` require the signed-in officer's activated teller session for the active posting business date, except assigned-session activation, counts, cash receipt decisions and handoff acknowledgement/reassignment, which enforce their own ownership and state checks. Teller-session creation is manager-only. Reads, self-service password change, and unrelated maintenance remain governed by their own authentication and permission rules. `POST /api/operations/business-date/start` requires the manager to provide the starting cash; it creates today's Myanmar business date, opening vault and optional schedules atomically, deactivating older dates without closing them. Date reads and End of Day close do not create a new date. A missing session for a teller business operation returns `CashSessionNotOpen`.

## Available interest rules

`GET /api/interest-rate-rules?accountTypeId={id}` returns rules for the selected product whose status is `Active` and whose effective date range includes the current Myanmar (Asia/Rangoon) business date. A positive unknown account type ID returns `AccountTypeNotFound`; a type with no applicable rules returns an empty array. Current accounts intentionally have no seeded rule.

`POST /api/accounting/reconciliation/accounts` accepts `fromDate`, `toDate`, and an optional `accountId`. It returns daily comparison snapshots for every eligible account across the inclusive date range. Ledger balances are cumulative through each date; operational balances use the latest account transaction snapshot through that date.

The inclusive date range is limited to 366 business dates. Exception status filters accept `Open`, `UnderInvestigation`, `AdjustmentRequired`, or `Resolved`, and updates must follow supported transitions. Invalid ranges, statuses, IDs, and overlong notes return HTTP 400 with a stable message code. Cash transfer/adjustment notes are limited to 500 characters and count notes to 2,000; amounts allow at most two decimal places.

## List accounts

`GET /api/accounts` returns a forward-only page ordered by account ID.

- `pageSize` defaults to 20 and accepts 1–100.
- `search` partially matches the account number.
- `accountTypeId` and `status` filter by product and `AccountStatus`.
- Filters must remain unchanged while following a cursor chain.
- Send the previous `nextCursor` as `cursor`; `nextCursor` is `null` on the final page.
- The response contains `items`, `hasMore`, and `nextCursor` and does not run a total-count query.

## Get an account

`GET /api/accounts/{id}` returns `AccountResponse`, including the current optimistic-lock `version`. Unknown IDs return `AccountNotFound`.

## Create an account

`POST /api/accounts` consumes `multipart/form-data`. Document fields use indexed form keys:

```text
Documents[0].DocumentType=Nrc
Documents[0].DocumentNumber=optional-number
Documents[0].File=<binary file>
RefererNrcs[0]=<existing customer NRC who owns an account>
```

To reuse an existing upload, send `Documents[0].SourceCustomerDocumentId=<id>` instead of `Documents[0].File`. Exactly one source is required per entry. Obtain IDs from the `reusableDocuments` array in `GET /api/accounts/opening-options` (add `secondHolderNrc` for joint accounts). Each item includes `id`, `customerId`, `holderName`, `documentType`, `documentNumber`, and `expiryDate`; storage paths are omitted. The server revalidates ownership, type, expiry, readability, and account file rules at submission and copies the source to the account's private directory. Upload-only requests remain supported.

Errors `3133 / AccountDocumentSourceInvalid` and `3134 / ReusableAccountDocumentUnavailable` identify invalid source combinations and unavailable sources respectively. On 3134, refresh opening options and request uploads for requirements no longer covered.

- The complete multipart request is limited to 60 MB by default.
- Each required document is one PDF, JPEG, or PNG file up to 10 MB.
- Seeded products require NRC, Photo, ProofOfAddress, HouseholdRegistration, and SourceOfFunds.
- Files are stored under `FileUploads:RootPath`; no public download endpoint is exposed.
- The authenticated user's JWT identity supplies audit attribution.
- A positive opening balance also requires that user's activated open teller session for the active posting business date. The server records a linked cash Deposit movement and increases the session's expected cash in the account-creation transaction. No session ID is accepted from the client. Missing or ineligible sessions return `CashSessionNotOpen`; zero balances create no deposit or cash movement and require no session.
- Referrer NRC rows must resolve to distinct existing customers who each own an account; the minimum is computed from the selected account type for each account holder. Referrer rows are saved with the account in the creation transaction.

Fixed-deposit creation additionally requires `interestRateRuleId`, `renewalInstruction`, and `calculateFromCurrent`. `payoutAccountId` is optional; if omitted, the server selects an active individual account owned by the primary holder whose account type is not fixed deposit. A supplied payout account must meet the same ownership, status, and account-type conditions. `RequiredProductId` is only an eligibility prerequisite for opening the selected account type and does not constrain the payout account. Fixed-deposit-only fields must be omitted for other products.

## Account status actions

The three status routes accept this JSON body:

```json
{
  "reason": "Optional audit reason",
  "version": 1
}
```

The route fixes the target status; clients cannot send an arbitrary status. Successful responses use `AccountStatusUpdatedSuccessfully` and contain the new account version. Invalid transitions return `AccountStatusTransitionNotAllowed`.

## Update account holders

`PUT /api/accounts/{id}/holders` replaces the editable state of both existing joint holders. The request contains:

- The parent account's `accountVersion`.
- Exactly two existing holder IDs.
- Each holder's current `version`, ownership percentage, and primary designation.
- One nullable signing rule shared by both holders.

The operation cannot add, remove, or replace customers. It requires exactly one primary holder and ownership percentages totaling 100.

## Update a fixed deposit

`PATCH /api/accounts/fixed-deposits/{fixedDepositId}` accepts the fixed-deposit `version` and at least one of `renewalInstruction` or `payoutAccountId`. The route ID is the fixed-deposit row ID, not the account ID.

`calculateFromCurrent` is immutable after creation. Current-principal and status changes are internal `IFixedDepositService` operations and are not exposed through HTTP.

## Concurrency and errors

Account, holder, and fixed-deposit responses include `version`. Mutation callers must echo the latest version. A stale version rejects the complete operation with HTTP 409 and `ConcurrentModification`; clients must refresh before retrying.

All successful responses use `ApiMessageResponse<T>`. Expected failures use `ApiErrorResponse` with `code`, `name`, `message`, and `traceId`. Clients must branch on `code`, not message text.

## Operations (per-customer)

Read-only lists for the desktop Operations pages. Every route requires the `operation` permission and returns
`ApiMessageResponse<PagedResponse<T>>`. Common query parameters: `search` (partial match on account number, or any
holder's customer number or name), `from` / `to` (inclusive `yyyy-MM-dd` dates), `page` (from 1) and `pageSize`
(default 10, at most 100). `from` after `to` returns 400 `InvalidDateRange`. The customer on each row is the
account's primary holder.

| Method | Route | Extra filters | Rows | Order |
| --- | --- | --- | --- | --- |
| GET | `/api/operations/interest` | `status` (`Accrued`, `Posted`); dates bound the period end | `InterestOperationResponse`: one monthly interest accrual | Newest period first |
| GET | `/api/operations/fees` | `feeType` (e.g. `Maintenance`, `EarlyWithdrawal`, `DormantAccount`), `status` (`FeeAccrualStatus`); dates bound the period end | `FeeOperationResponse`: one fee accrual | Newest period first |
| GET | `/api/operations/fixed-deposit-maturity` | `status` (`FixedDepositStatus`); dates bound the maturity date | `FixedDepositMaturityResponse`: one fixed deposit | Nearest maturity first |

Each list has a detail route returning `ApiMessageResponse<T>`; an unknown id returns 404 with the code shown.

| Method | Route | Response | Not found |
| --- | --- | --- | --- |
| GET | `/api/operations/interest/{id}` | `InterestOperationDetailResponse`: account and customer contact (`OperationAccountDetail`), rule id, and the accrual and credit transactions (`OperationTransactionLink`, null until written) | 4243 `InterestAccrualNotFound` |
| GET | `/api/operations/fees/{id}` | `FeeOperationDetailResponse`: account and customer contact, fee rule (amount or percentage), and the accrual and deduction transactions | 4244 `FeeAccrualNotFound` |
| GET | `/api/operations/fixed-deposit-maturity/{id}` | `FixedDepositMaturityDetailResponse`: account and customer contact, original and current principal, term, accrued / credited / expected interest, payout account, and the month-by-month `interestSchedule` | 4205 `FixedDepositNotFound` |

The credit or deduction transaction is the quarterly posting, so its amount can cover several months.

`FixedDepositMaturityResponse` adds `daysToMaturity` (from today's Myanmar business date, zero or negative once
matured), `interestAccrued` (accruals inside the deposit's term) and `expectedMaturityInterest` (current principal ×
rate × term days / 365, rounded to 2 decimals, the same convention as the interest job).

## Scheduled accounting-close recovery

The manager permission `scheduled_job_management` protects these endpoints:

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/operations/scheduled-jobs/failures` | Lists occurrences that exhausted automatic attempts and remain unresolved. Includes the stable failure code and a safe cause summary. |
| POST | `/api/operations/scheduled-jobs/executions/{executionId}/retry` | Queues another attempt for the same occurrence and period; records the requesting user and timestamp. |

For an open previous-month business date, the response includes `BusinessDateToClose`; close it through the existing `/api/operations/business-date` pre-close, approval, and close workflow before retrying. The retry endpoint does not close dates or bypass EOD gates. A retry rejection uses `ScheduledJobRetryUnavailable`; an unclosed month uses `MonthlyAccountingPeriodNotClosed`.

## Unified reconciliation and cash correction

The desktop reconciliation workspace and End of Day use these unified endpoints. A check produces account/GL and cash exceptions; listing returns results for the requested date range. The UI presents active items in separate tabs. Assignment is restricted to authorized staff, investigators retrieve only their assigned tasks, and Force Pass requires manager approval permission and a reason. A Force Passed exception is excluded while the captured source remains unchanged; source changes can create a new exception.

| Method | Route | Permission | Purpose |
| --- | --- | --- | --- |
| POST | `/api/reconciliation/checks` | `accounting`, `audit`, or `end_of_day_approval` | Run account/GL and cash reconciliation for the request scope. |
| GET | `/api/reconciliation/exceptions?fromDate={date}&toDate={date}` | `accounting`, `audit`, or `end_of_day_approval` | Read reconciliation results for a date range. |
| GET | `/api/reconciliation/exceptions/{id}/detail` | `accounting`, `reconciliation_investigation`, or `end_of_day_approval` | Load exception details, subject to assignment/access rules. |
| POST | `/api/reconciliation/exceptions/{id}/assign` | `accounting` or `transaction_correction_approval` | Assign an eligible investigator. |
| GET | `/api/reconciliation/tasks/mine` | `reconciliation_investigation` | List the caller's assigned investigation tasks. |
| POST | `/api/reconciliation/exceptions/{id}/force-pass` | `transaction_correction_approval` | Force Pass an exception with a required reason. |
| GET | `/api/reconciliation/account-options?search={term}` | `accounting` or `reconciliation_investigation` | Search reconciliation account options. |
| GET | `/api/reconciliation/staff-options` | `accounting` or `transaction_correction_approval` | List eligible staff for assignment. |
| GET | `/api/reconciliation/cash/discrepancies` | `reconciliation_investigation` | List cash discrepancy types and their input definitions. |
| POST | `/api/reconciliation/cash/exceptions/{id}/preview` | `reconciliation_investigation` | Validate diagnosis inputs and calculate a suggested correction. |
| POST | `/api/reconciliation/cash/exceptions/{id}/proposals` | `reconciliation_investigation` | Submit cash correction inputs and the preview hash returned by the preview endpoint. |
| GET | `/api/reconciliation/cash/proposals/pending` | `transaction_correction_approval` | List cash proposals awaiting manager review. |
| POST | `/api/reconciliation/cash/proposals/{id}/review` | `transaction_correction_approval` | Approve/post or reject a cash proposal. |
| GET | `/api/reconciliation/verification` | `reconciliation_investigation` or `transaction_correction_approval` | List executed account/GL and cash corrections with pending or failed verification; investigators see assigned work and approvers see all tasks. |
| POST | `/api/reconciliation/cash/proposals/{id}/verify` | `reconciliation_investigation` or `transaction_correction_approval` | Retry checks after posting, restricted to the assignee or a correction approver. No financial posting is replayed. |
| POST | `/api/reconciliation/vault/final-count` | `end_of_day_approval` | Record the final physical vault count for a business date. |

Account/GL diagnosis and proposal routes are documented in [Account reconciliation correction workflow](#account-reconciliation-correction-workflow). Approval appends linked financial/accounting/GL records where appropriate; original records are preserved. Cash count observations do not overwrite expected cash. A count-entry error is resolved by recount. Other cash corrections use server-calculated previews and manager-approved operational postings. Preview hashes bind the submitted inputs to the displayed preview; stored proposal source hashes track source records only, independent of JSON input serialization. If the cash proposal source hash has changed by manager review, the proposal is marked `Superseded`, the API returns `CorrectionProposalStale`, and the investigator may submit a refreshed proposal. A normally closed business date returns `BusinessDateClosed`; a force-closed source redirects financial posting to the active open date. A sole active Manager may self-approve only when that manager is also the proposal author and holds the required permission.

## Bank expense recording

Corrections for force-closed dates post on the current active open date. Cash previews return `postingDate` separately from their source `businessDate`; cash proposal responses expose `sourceBusinessDate` and `postingDate`. Expense responses retain `businessDate` as the source date and expose the financial `postingDate`. Account/GL proposal lists display the effective execution date in their preview; approval persists the executed plan and links the transaction to the original exception. A missing active date blocks approval without changing the pending proposal. Financial records and finalized summaries of the closed date are preserved.

| Method | Route | Permission | Purpose |
| --- | --- | --- | --- |
| GET | `/api/cash-operations/expenses?date={date}` | `cash_operations`, `end_of_day_approval`, or `audit` | List bank expenses, optionally for one date. |
| POST | `/api/cash-operations/expenses` | `cash_operations` or `end_of_day_approval` | Record an expense request with session, amount, payee, purpose, and receipt or missing-receipt reason. |
| POST | `/api/cash-operations/expenses/{id}/review` | `end_of_day_approval` | Approve or reject the expense. Approval posts balanced expense and cash entries. |
| GET | `/api/cash-operations/expenses/{id}/receipt` | `cash_operations`, `end_of_day_approval`, or `audit` | Download an authorized expense receipt. |

Receipt uploads accept the implemented image/PDF formats and size limit; when no receipt is supplied, a reason is required. Approval creates the Bank Operating Expense debit, Cash on Hand credit, and corresponding cash movement atomically.
## Teller session cash handoffs

Cash session close counts may include `handoffRecipientUserId` when `actualAmount` is positive. This closes the session and creates a pending custody transfer in the same database transaction. A positive count without a valid eligible recipient is rejected; a zero count needs no recipient. Recounting an already closed session does not create a second handoff.

Session opening and count-close requests require the standard `Idempotency-Key` header (maximum 64 characters). A retry with the same key and same payload returns the original session/count result; reuse with different details returns `IdempotencyKeyReused`. Cash-count requests also send `expectedSessionVersion`; handoff action/reassignment requests send `expectedVersion`. Responses include the latest `version`. A stale version returns HTTP 409 `ConcurrentModification`; refresh the selected session/handoff before making a new change. Repeated accept/decline requests in their already-applied state are treated as successful replays and do not append duplicate history.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/cash-handoffs/recipients` | Lists active eligible recipients (cash operations, audit, or EOD approval). |
| GET | `/api/cash-handoffs?businessDate=YYYY-MM-DD` | Lists handoffs visible to the sender/recipient or an authorized reviewer. |
| GET | `/api/cash-handoffs/{id}` | Returns a visible handoff and its append-only history. |
| POST | `/api/cash-handoffs/{id}/accept` | Recipient acknowledges custody; optional note. |
| POST | `/api/cash-handoffs/{id}/decline` | Recipient declines with a required note; remains an EOD blocker. |
| PUT | `/api/cash-handoffs/{id}/recipient` | Sender or audit/EOD reviewer assigns an eligible recipient; status becomes pending. |

All routes require at least one of `cash_operations`, `audit`, or `end_of_day_approval`; business authorization is further restricted by actor and handoff state in the service. `CashHandoffRecipientRequired` and `CashHandoffNotPending` are stable error codes.


## Account reconciliation correction workflow

All routes below also require `accounting`. Investigation endpoints require `reconciliation_investigation`; approval requires `transaction_correction_approval`. Request and response bodies use the DTOs shown in their route handlers.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/accounting/reconciliation/workflow/discrepancies` | Returns discrepancy types and required proposal inputs. |
| POST | `/api/accounting/reconciliation/workflow/checks` | Runs account, journal, and daily debit/credit checks; creates or updates exceptions. |
| POST | `/api/accounting/reconciliation/workflow/exceptions/{id}/investigation` | Assigns an active investigator and records findings, diagnosis, and review date. |
| GET | `/api/accounting/reconciliation/workflow/transactions/{id}/evidence` | Returns original account and GL entries, including linked journal repairs. |
| GET | `/api/accounting/reconciliation/workflow/gl-options` | Lists active GL accounts for investigator input. |
| POST | `/api/accounting/reconciliation/workflow/exceptions/{id}/preview` | Validates diagnosis inputs and returns a server-calculated correction preview and source hash. |
| POST | `/api/accounting/reconciliation/workflow/exceptions/{id}/proposals` | Saves a draft or submits an immutable proposal. Supply the preview source hash when submitting. |
| POST | `/api/accounting/reconciliation/workflow/exceptions/{id}/proposals/{draftId}` | Saves changes to the proposer?s draft. Submitted proposals cannot be edited. |
| GET | `/api/accounting/reconciliation/workflow/proposals` | Lists proposal history, optionally filtered by exception. |
| GET | `/api/accounting/reconciliation/workflow/proposals/{id}` | Gets proposal evidence, preview, review, execution, and verification status. |
| POST | `/api/accounting/reconciliation/workflow/proposals/{id}/review` | Approves and executes atomically, or rejects with a reason. |
| POST | `/api/accounting/reconciliation/workflow/proposals/{id}/verify` | Retries verification without replaying the financial posting; requires investigation or correction approval permission and assignment or correction approver access. |
| GET | `/api/accounting/reconciliation/workflow/summaries?date=YYYY-MM-DD` | Lists provisional and final summary revisions. |
| POST | `/api/accounting/reconciliation/workflow/summaries/{date}/refresh` | Creates new provisional revisions for open dates from the given date forward. |

`CorrectionInputs` selects the discrepancy kind and supplies investigation findings, evidence reference, source transaction, and any case-specific correction inputs. The client never supplies authoritative journal lines. Approval revalidates the proposal source hash. A changed source requires a refreshed proposal and manager review.

Legacy `/correction-requests` and `/correction-review` routes reject new calls with `CorrectionLegacyResubmit`; pending legacy requests are marked superseded during migration. Historical correction records remain available.


`BusinessDateResponse` includes `isActive` and `canStartNewDate`. Only the active open date accepts ordinary postings. A new date can be started while an earlier date remains open; that prior date becomes inactive and awaits investigation/EOD finalization.

### Cash correction source evidence

`CashCorrectionInputs` for `/api/reconciliation/cash/exceptions/{id}/preview` and `/proposals` now includes nullable `counterpartMovementId` and `handoffId`. Counterpart is required for duplicate, transfer and handoff discrepancies; for duplicates it identifies the retained movement. Handoff discrepancies additionally require an accepted handoff ID. Unrelated source identifiers are rejected. `CashCorrectionPreview.positionEffects` lists `{ sessionId, cashChange }` for every affected position, including the destination. Amount corrections specify a corrected total rather than a signed delta. See [cash discrepancy operations](CashDiscrepancyCorrections.md) for complete validation and verification rules.

### EOD approval and close responses

`POST /api/operations/business-date/runs/{id}/force-close-approve` returns a Closed run when the last required approval also completes closure. Clients must reload the selected business-date record after any force-close request/approval returning Closed. The same run is returned on a completed-close retry; duplicate force-approval retries do not append approvals or repeat financial finalization. The pre-close action also accepts end_of_day_approval permission so an eligible manager can prepare and complete the sole-manager flow. Normal approval and normal Close remain separate operations.

`EndOfDayRunResponse.pendingForceClose` optionally carries the still-pending override when latest-run returns a newer normal run. Clients can run normal checks alongside pending override approval, but cannot create another pending override. On successful normal close the pending override becomes OverrideCancelled; on successful force close unfinished normal attempts become Superseded. Attempts to operate a stopped run are rejected with existing transition/approval codes. Latest-run prefers the completed closure after either path wins.

### Investigator sources, manager evidence and persisted audits

| Method | Route | Permission and response |
| --- | --- | --- |
| GET | `/api/reconciliation/cash/exceptions/{id}/sources?search=...` | reconciliation_investigation plus assigned/accounting access to the exception. Returns CashSourceCatalog with active customer accounts, missing-movement transactions, eligible original movements, same-date positions, accepted handoffs and source journal lines. Search matches source IDs, account/transaction numbers or movement types; lists are limited to 500 per category. |
| GET | `/api/reconciliation/cash/proposals/{id}/evidence` | transaction_correction_approval. Returns original journal lines, current server preview, source-hash comparison and warning. An outdated preview is read-only evidence; it cannot authorize approval. |
| GET | `/api/reconciliation/audit/{date}` | accounting, audit or end_of_day_approval. Returns a nullable EndOfDayAuditReport with raw/effective totals, reconciliation status and typed missing-journal, raw/combined journal and waiver findings. It reads persisted results and never reruns EOD. |

CashCorrectionPreview now includes server-calculated JournalLines, AccountEffects and PositionEffects with before/after expected and counted cash. Position effects use the original custody occurrence time when predicting the counted-cash result. CashSourceCatalog selections do not grant posting permission: the server still validates every source and approval. A repeated cash approval of an already Executed proposal returns its existing execution and verification state; no records are reposted.


## Session Management contracts

- `GET /api/operations/business-date` includes server `today` and accurate `canStartNewDate`, even with an older active date.
- `POST /api/operations/business-date/start` accepts `startingCash`, optional `sessions`, `idempotencyKey`, and `expectedDate`. Rows contain `officerId`, `startAtMyanmar`, `endAtMyanmar`, `startingCash`. Times are unspecified-kind Myanmar values on today. The response includes `vault` and `sessions`. Retries must match actor, key, vault cash and all schedule inputs.
- `POST /api/cash-operations/sessions/schedules` accepts `sessions`, `idempotencyKey`, and optional `expectedDate`. Managers create and reserve the complete batch atomically. This endpoint requires rows; opening may omit them.
- `GET /api/cash-operations/sessions?businessDate=yyyy-MM-dd` defaults to today and scopes officers to their assignments. Responses include officer name, scheduled starting cash, closure metadata and `canAccept`, `canRequestCash`, `canClose`, `canCancel` hints. Mutations revalidate eligibility.
- `POST /api/cash-operations/sessions/{id}/activate` atomically activates and receives initial cash for the assigned officer within the period on today's active date.
- `POST /api/cash-operations/sessions/{id}/cancel-scheduled` retains status Cancelled and closure actor/time, resolves the initial proposal and releases reserved cash. Cancelled sessions do not block EOD.
- `GET /api/cash-operations/sessions/{id}` includes transactions linked through cash movements and session cash requests, preserving movements and counts.
- `GET /api/cash-handoffs/recipients?sourceSessionId={id}` filters recipients for the authorized source. Officers need an active same-date session and cannot receive older-date handoffs. Manager receipt returns teller cash to the source date's open vault; auditor receipt records custody only. Reassignment clears the previous destination.

No schema migration is needed: cancellation uses existing status/closure fields, and batch keys use existing session idempotency keys. Deploy server and desktop together because activation now includes initial cash receipt. Historical records are retained. Scheduled reconciliation preserves its explicit occurrence-date behavior without requiring an open business date.
