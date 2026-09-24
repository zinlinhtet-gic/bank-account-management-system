# API Contracts

The machine-readable contract is [AccountsApi.openapi.yaml](AccountsApi.openapi.yaml). This document summarizes the routes currently exposed by `AccountsController` and `AccountTypesController`.

## Endpoint overview

| Method | Route | Authentication | Purpose |
| --- | --- | --- | --- |
| GET | `/api/account-types` | Public | List active account products. |
| GET | `/api/accounts` | `account_management` | List accounts with cursor pagination. |
| GET | `/api/accounts/{id}` | `account_management` | Get one account. |
| POST | `/api/accounts` | `account_management` | Create an account. |
| PATCH | `/api/accounts/{id}/freeze` | `account_management` | Freeze an Active or Dormant account. |
| PATCH | `/api/accounts/{id}/suspend` | `account_management` | Suspend an Active or Dormant account. |
| PATCH | `/api/accounts/{id}/reactivate` | `account_management` | Reactivate a Dormant, Suspended, or Frozen account. |
| PUT | `/api/accounts/{id}/holders` | `account_management` | Update both holders of a joint account. |
| PATCH | `/api/accounts/fixed-deposits/{fixedDepositId}` | `account_management` | Update payout and renewal instructions. |

## Available account types

`GET /api/account-types` returns active products ordered by ID. Each `AccountTypeResponse` includes opening and maintained balances, transaction limits and capabilities, required-product information, and fixed-deposit classification.

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
```

- The complete multipart request is limited to 60 MB by default.
- Each required document is one PDF, JPEG, or PNG file up to 10 MB.
- Seeded products require NRC, Photo, ProofOfAddress, HouseholdRegistration, and SourceOfFunds.
- Files are stored under `FileUploads:RootPath`; no public download endpoint is exposed.
- The authenticated user's JWT identity supplies audit attribution.

Fixed-deposit creation additionally requires `interestRateRuleId`, `renewalInstruction`, and `calculateFromCurrent`. `payoutAccountId` is optional; if omitted, the primary holder's eligible individual payout account is selected. Fixed-deposit-only fields must be omitted for other products.

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

`PATCH /api/accounts/fixed-deposits/{fixedDepositId}` accepts the last-read `version` together with `renewalInstruction`, `payoutAccountId`, or both. The route identifier is the fixed-deposit row ID, not the account ID. The response wraps the updated `FixedDepositResponse` with message code `1104`.

`CalculateFromCurrent` is fixed when the deposit is created. Current-principal and status changes are available only to internal application workflows through `IFixedDepositService`; they are not accepted by an HTTP request contract.

Account, account-holder, and fixed-deposit responses include a `version`. Mutation callers must return the latest version they received. A stale account, holder, or fixed-deposit version rejects the complete operation with HTTP 409 and message code `4304`; clients must refresh the resource before retrying.

All account mutation endpoints require the `account_management` permission. Audit logs and account-status history resolve the acting user from the standard JWT name-identifier claim; a missing or invalid identity is rejected with message code `4000` and HTTP 401.
Update endpoints resolve the acting user from the standard name-identifier claim. While authentication integration is incomplete, requests without a valid claim temporarily use development user ID `1` for audit attribution.
