# API Contracts

The machine-readable contract is [AccountsApi.openapi.yaml](AccountsApi.openapi.yaml). This document summarizes the routes currently exposed by account, customer, account-type, interest-rate, accounting, cash-operations, and business-date controllers.

## Endpoint overview

Every successful endpoint returns `ApiMessageResponse<T>` with the endpoint payload in `data`; this includes list and cursor-paginated responses. The desktop client unwraps this envelope through `ApiClient`.

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
| GET | `/api/cash-operations/sessions` | `cash_operations` | List teller/vault sessions for a business date. |
| GET | `/api/cash-operations/sessions/{id}` | `cash_operations` | Read an authorized cash session with its movement and physical-count history and linked transaction references. |
| POST | `/api/cash-operations/sessions` | `cash_operations` | Open a teller or vault cash position. |
| POST | `/api/cash-operations/sessions/{id}/transfers` | `cash_operations` | Transfer expected cash between positions on the same business date. Cash positions are not branch-scoped. |
| POST | `/api/cash-operations/sessions/{id}/count` | `cash_operations` | Record physical cash count and create a mismatch exception. |
| POST | `/api/cash-operations/sessions/{id}/adjustments` | `cash_operations` | Submit a signed adjustment linked to the exact posted Cash on Hand journal effect. |
| GET | `/api/cash-operations/adjustments` | `cash_operations` and `end_of_day_approval` | List cash adjustment requests for approval review. |
| POST | `/api/cash-operations/adjustments/{id}/approve` | `cash_operations` and `end_of_day_approval` | Approve another user's valid adjustment and apply it to the expected position. |
| GET | `/api/operations/business-date` | `accounting`, `audit`, or `cash_operations` | Read or initialize the persisted open business date. |
| POST | `/api/operations/business-date/{date}/pre-close` | `accounting` or `audit` | Run and persist EOD pre-close stages. |
| POST | `/api/operations/business-date/runs/{id}/approve` | `end_of_day_approval` | Approve a run as a different user than the preparer. |
| POST | `/api/operations/business-date/runs/{id}/close` | `end_of_day_approval` | Close after locking and rechecking blockers; open the next date. |

## Available account types

`GET /api/account-types` returns active products ordered by ID. Each `AccountTypeResponse` includes opening and maintained balances, transaction limits and capabilities, required-product information, fixed-deposit classification, allowed customer types, and customer-type-specific referrer minimums.

`GET /api/accounts/customer-lookup?nrc={nrc}` returns the matching customer, including `KycStatus`. Every holder must be `Verified` before an account can be created; the server enforces this rule even when the account API is called directly. `GET /api/accounts/opening-options` filters products by all selected holders' customer types and account-type eligibility, applies each product's `RequiredProductId` rule to customer eligibility (an active account of that type must be owned by at least one selected customer), excludes account types already actively held by the selected customer or by both selected customers in a shared application, includes product document requirements, and returns the primary holder's active individual non-fixed-deposit accounts as payout choices. The desktop uses those payout choices directly; the server independently validates the payout owner, ownership type, active status, and non-fixed-deposit account type when creating the account. Required products and payout accounts are separate rules. `POST /api/customers` is the Customer Management creation endpoint; it accepts multipart form data and returns a full `CustomerResponse`. New customers need a verified KYC review before account opening.

Account transaction, status-history, and interest-accrual routes return the records for one existing account ordered newest first.

Transaction summaries, detail, and posting responses include `businessDate` separately from the UTC `transactionAt` timestamp.

Cash funded deposit, withdrawal, NRC transfer, NRC pickup, and cash refund requests are associated server-side with the actor's open teller session for the active posting business date. Transaction request DTOs do not require or accept a cash-session identifier; the physical movement is saved atomically with posting. If no current teller session exists, the server returns `CashSessionNotOpen`. A physical count is immutable and never overwrites expected cash. Cash adjustments require a signed amount equal to a posted transaction's Cash on Hand journal effect and independent approval. Account reconciliation preserves run/result snapshots and exception history; mismatches are resolved only after a matched rerun. EOD initiation is manual; scheduled reconciliation and financial postings use the existing scheduled-job mechanism.

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

- The complete multipart request is limited to 60 MB by default.
- Each required document is one PDF, JPEG, or PNG file up to 10 MB.
- Seeded products require NRC, Photo, ProofOfAddress, HouseholdRegistration, and SourceOfFunds.
- Files are stored under `FileUploads:RootPath`; no public download endpoint is exposed.
- The authenticated user's JWT identity supplies audit attribution.
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

## Scheduled accounting-close recovery

The manager permission `scheduled_job_management` protects these endpoints:

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/operations/scheduled-jobs/failures` | Lists occurrences that exhausted automatic attempts and remain unresolved. Includes the stable failure code and a safe cause summary. |
| POST | `/api/operations/scheduled-jobs/executions/{executionId}/retry` | Queues another attempt for the same occurrence and period; records the requesting user and timestamp. |

For an open previous-month business date, the response includes `BusinessDateToClose`; close it through the existing `/api/operations/business-date` pre-close, approval, and close workflow before retrying. The retry endpoint does not close dates or bypass EOD gates. A retry rejection uses `ScheduledJobRetryUnavailable`; an unclosed month uses `MonthlyAccountingPeriodNotClosed`.
