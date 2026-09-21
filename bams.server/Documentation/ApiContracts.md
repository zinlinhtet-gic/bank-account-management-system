# API Contracts

The machine-readable OpenAPI contract for the routes currently exposed by `AccountsController` and `AccountTypesController` is maintained in `AccountsApi.openapi.yaml`.

## List Accounts

`GET /api/accounts` returns account summaries using forward-only cursor pagination ordered by account ID. The first request omits `cursor`; subsequent requests send the opaque `nextCursor` returned by the previous response.

The optional query parameters are `search` for a partial account-number match, `accountTypeId` for an account-type identifier, and `status` for an `AccountStatus` enum name. Filters are combined and must remain unchanged while following a cursor chain. `pageSize` defaults to 20 and accepts values from 1 through 100.

The response contains `items`, `hasMore`, and `nextCursor`. `nextCursor` is `null` when no further matching accounts exist. The endpoint intentionally does not execute a total-count query or return page numbers.

## Available Account Types

`GET /api/account-types` returns all account products whose status is `Active`, ordered by identifier. The response contains account-opening limits, transaction capabilities, required-product information, and fixed-deposit classification without exposing the database entity directly.

## Create Account

`POST /api/accounts` consumes `multipart/form-data`.

Account fields retain their existing names. Documents use indexed keys:

```text
Documents[0].DocumentType=Nrc
Documents[0].DocumentNumber=optional-number
Documents[0].File=<binary file>
```

The authenticated user's JWT name-identifier claim supplies the user ID for the account-opening audit record. Clients cannot supply or override audit attribution.
`CreatedBy` is the numeric user identifier responsible for opening the account and is included in the account-opening audit record.

Repeat the indexed group for each required document. Each document type may appear once. Supported file formats are PDF, JPEG, and PNG, with a maximum size of 10 MB per file.

The complete multipart request is limited to 60 MB by default.

The six seeded account types are Current, Normal Saving, Special Saving, Normal Deposit, Special Deposit, and Hundred-Days Deposit. Each requires NRC, Photo, ProofOfAddress, HouseholdRegistration, and SourceOfFunds documents.

Files are stored beneath the configured `FileUploads:RootPath` with GUID-generated names. The API does not expose a public document-download endpoint.

## Account Status Actions

`PATCH /api/accounts/{id}/freeze`, `PATCH /api/accounts/{id}/suspend`, and `PATCH /api/accounts/{id}/reactivate` accept the last-read `version` and an optional `reason`. The route selects `AccountStatus.Frozen`, `AccountStatus.Suspended`, or `AccountStatus.Active`; clients cannot supply a status value in the request body. Each response wraps the updated `AccountResponse` with message code `1101`.

## Adjust Account Balance

`PATCH /api/accounts/{id}/balance` accepts an `amount` and the last-read `version`. The amount is an adjustment rather than an absolute balance: positive values deposit funds and negative values withdraw funds. An adjustment that would make the balance negative is rejected. The response wraps the updated `AccountResponse` with message code `1102`.

## Update Account Holders

`UpdateHoldersOfAccountAsync` accepts an account identifier and an `UpdateAccountHoldersRequest`. The request contains the account version, exactly two existing account-holder IDs, each holder's version, complete ownership percentage and primary designation, and one nullable signing rule shared by both holders.

`PUT /api/accounts/{id}/holders` exposes the operation for non-closed joint accounts and wraps the two updated `AccountHolderResponse` records with message code `1103`. It does not add, remove, or replace customers.

## Update Fixed Deposit

`PATCH /api/accounts/fixed-deposits/{fixedDepositId}` accepts the last-read `version` together with `renewalInstruction`, `payoutAccountId`, or both. The route identifier is the fixed-deposit row ID, not the account ID. The response wraps the updated `FixedDepositResponse` with message code `1104`.

`CalculateFromCurrent` is fixed when the deposit is created. Current-principal and status changes are available only to internal application workflows through `IFixedDepositService`; they are not accepted by an HTTP request contract.

Account, account-holder, and fixed-deposit responses include a `version`. Mutation callers must return the latest version they received. A stale account, holder, or fixed-deposit version rejects the complete operation with HTTP 409 and message code `4304`; clients must refresh the resource before retrying.

All account mutation endpoints require the `account_management` permission. Audit logs and account-status history resolve the acting user from the standard JWT name-identifier claim; a missing or invalid identity is rejected with message code `4000` and HTTP 401.
Update endpoints resolve the acting user from the standard name-identifier claim. While authentication integration is incomplete, requests without a valid claim temporarily use development user ID `1` for audit attribution.
