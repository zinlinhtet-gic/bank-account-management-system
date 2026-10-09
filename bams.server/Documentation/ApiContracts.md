# API Contracts

The machine-readable contract is [AccountsApi.openapi.yaml](AccountsApi.openapi.yaml). This document summarizes the routes currently exposed by account, customer, account-type, and interest-rate controllers.

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

## Available account types

`GET /api/account-types` returns active products ordered by ID. Each `AccountTypeResponse` includes opening and maintained balances, transaction limits and capabilities, required-product information, fixed-deposit classification, allowed customer types, and customer-type-specific referrer minimums.

`GET /api/accounts/customer-lookup?nrc={nrc}` returns the matching customer, including `KycStatus`. Every holder must be `Verified` before an account can be created; the server enforces this rule even when the account API is called directly. `GET /api/accounts/opening-options` filters products by all selected holders' customer types and account-type eligibility, applies each product's `RequiredProductId` rule to customer eligibility (an active account of that type must be owned by at least one selected customer), excludes account types already actively held by the selected customer or by both selected customers in a shared application, includes product document requirements, and returns the primary holder's active individual non-fixed-deposit accounts as payout choices. The desktop uses those payout choices directly; the server independently validates the payout owner, ownership type, active status, and non-fixed-deposit account type when creating the account. Required products and payout accounts are separate rules. `POST /api/customers` is the Customer Management creation endpoint; it accepts multipart form data and returns a full `CustomerResponse`. New customers need a verified KYC review before account opening.

Account transaction, status-history, and interest-accrual routes return the records for one existing account ordered newest first.

## Available interest rules

`GET /api/interest-rate-rules?accountTypeId={id}` returns rules for the selected product whose status is `Active` and whose effective date range includes the current Myanmar (Asia/Rangoon) business date. A positive unknown account type ID returns `AccountTypeNotFound`; a type with no applicable rules returns an empty array. Current accounts intentionally have no seeded rule.

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

## Operations (per-customer)

Read-only lists for the desktop Operations pages. Every route requires the `operation` permission and returns
`ApiMessageResponse<PagedResponse<T>>`. Common query parameters: `search` (partial match on account number, or any
holder's customer number or name), `from` / `to` (inclusive `yyyy-MM-dd` dates), `page` (from 1) and `pageSize`
(default 20, at most 100). `from` after `to` returns 400 `InvalidDateRange`. The customer on each row is the
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
