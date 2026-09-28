# Architecture

## Application flow

The server follows a layered ASP.NET Core design:

```text
HTTP request → Controller → Request DTO → Service → Entity Framework model → MySQL
MySQL → Entity Framework model → Service mapping → Response DTO → HTTP response
```

Controllers handle routing, binding, authorization attributes, and response envelopes. Services own validation, business rules, persistence coordination, auditing, and entity-to-DTO mapping. Database entities are never returned directly by controllers.

## Account subsystem

- `AccountsController` exposes account queries, creation, freeze/suspend/reactivate actions, joint-holder updates, and fixed-deposit configuration updates.
- `AccountTypesController` exposes the read-only catalog of active account products.
- `InterestRateRulesController` exposes active, currently effective rates filtered by account type.
- `AccountService` coordinates account operations and delegates holder, account-type, fixed-deposit, document, audit, transaction, and accounting responsibilities.
- `CustomerLookUpService` centralizes NRC-based customer resolution for account APIs and account-holder workflows.
- `CustomersController` (`api/customers`) and `CustomerCreationService` create persisted customer records; account opening uses the returned profile through the existing NRC lookup and opening-options APIs.
- `AccountHolderService` owns holder resolution, ownership validation, creation, joint-holder updates, and owned individual-account options.
- `AccountRefererService` resolves referrers by NRC, verifies that each already owns an account, and associates them with a newly created account in its database transaction.
- `AccountTypeService` owns product lookup, holder eligibility, opening-balance validation, and fixed-deposit classification.
- `AccountDocumentService` validates uploads, returns product document requirements, and coordinates private file storage.
- `AccountTransactionService` owns account transaction reads and account-opening transaction recording.
- `AccountStatusHistoryService` owns account status-history reads.
- `FixedDepositService` owns fixed-deposit creation, payout validation, editable instructions, principal/status lifecycle operations, maturity, and renewal.

`GetAccountOpeningOptionsAsync` stays in `AccountService` as the account-opening response composer. It delegates customer resolution, product eligibility, document requirements, and payout-account queries to their focused services; its HTTP response shape is unchanged.
User Management follows the same layers:

- `UsersController` (`api/users`, `[RequirePermission(user_management)]` on the class): list, roles, get, create,
  update, `reset-password`, soft delete.
- `IUserService` / `UserService`: validation, duplicate checks, last-manager rule, soft delete (rules in
  `BusinessRules.md`).
- DTOs under `DTO/Users`, mapping in `Mapping/UserMappings.cs`, limits and default passwords in
  `Constants/UserConstants.cs`.
- Shared helpers: `Utils/Security/PasswordHasher` (the only password hashing code) and
  `Utils/Extensions/UserStatusExtensions.EnsureCanSignIn()` (used by login, the auth endpoints and `[RequirePermission]`).

## WPF dialogs

`IDialogService.Confirm` shows a yes/no confirmation; `IDialogService.ShowDialog(IDialogViewModel)` hosts any dialog
ViewModel (forms, detail cards) in `Components/ModalDialog`, with the view picked from `Views/DialogTemplates.xaml`.
Both dim and blur the main window.

`ISessionService` owns the signed-in session: `StartSession()` (called when the main app is shown) sends the presence
heartbeat every 15 seconds; `EndSessionAsync()` stops it, calls `api/auth/logout` (best effort, 3 s timeout), clears the
token and `AuthContext`, and returns to sign-in. Use it for logout and for self-delete / own-role change.

## Authentication and authorization errors

## Authentication and authorization

Protected account endpoints require `SecurityConstants.AccountManagement` through `[RequirePermission]`.

- Missing or unknown users produce HTTP 401.
- Disabled users or users without the required permission produce HTTP 403.
- `CurrentUserService` obtains the authenticated user ID from the JWT name-identifier claim.
- Clients cannot supply audit attribution.

Authentication and authorization failures use `ApiErrorResponse`. `GlobalExceptionHandler` converts expected application exceptions into stable message codes and HTTP statuses.

## Persistence and consistency

- EF Core uses MySQL and applies schema migrations during startup.
- `DateOnly` converters keep nullable and non-nullable dates stored as MySQL `date` columns.
- `Account`, `AccountHolder`, and `FixedDeposit` use application-managed optimistic concurrency versions.
- Stale writes raise `DbUpdateConcurrencyException`, returned as HTTP 409 with `ConcurrentModification`.
- Audit entities are tracked without committing independently so the owning operation controls the transaction boundary.
- Joint-holder and fixed-deposit lifecycle writes keep business data and audit records atomic.

## Files and reference data

Account documents are validated against `AccountTypeRequiredDocument`. Files are stored below the configured private upload root, outside `wwwroot`, using generated filenames; the database stores only relative references and metadata.

After migrations, the idempotent `ProductSeeder` populates reference products, required documents, and demo interest rules. In Development only, `TestDataSeeder` adds deterministic sample customers and accounts.

## Desktop client integration

`bams.desktop/Api/ApiClient.cs` sends requests, unwraps `ApiMessageResponse<T>.Data`, and converts `ApiErrorResponse` into `ApiException`. Transport failures become `NetworkException`. ViewModels handle stable message codes rather than comparing message text.
