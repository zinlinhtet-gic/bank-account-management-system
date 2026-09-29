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

## Scheduled jobs

`JobsHostedService` polls registered schedules and delegates work to scoped service functions through `JobsOperationService`. Job definitions and execution attempts persist in `ScheduledJobs` and `ScheduledJobExecutions`; execution rows record running, succeeded, failed, or cancelled attempts and error details. Database leases prevent multiple server instances from executing the same occurrence concurrently.

The monthly account maintenance and interest accumulation handlers run on day 5 at Myanmar midnight and page through product-category account batches. `ScheduledFinancialPostingService` records accrual and settlement transactions, balanced ledger entries, account transactions, and system-attributed audit records atomically.

Register an existing service method directly in `Program.cs`:

```csharp
builder.Services.AddScheduledJobs(builder.Configuration, jobs =>
{
    jobs.Add<ReportService>(
        "report-every-five-minutes",
        JobSchedule.Every(TimeSpan.FromMinutes(5)),
        (service, cancellationToken) => service.GenerateReportAsync(cancellationToken));

    jobs.Add<ReportService>(
        "monthly-report",
        "Monthly report",
        JobSchedule.Monthly(1, TimeSpan.Zero, "Myanmar Standard Time"),
        (service, execution, cancellationToken) =>
            service.GenerateMonthlyReportAsync(execution.ScheduledForUtc, cancellationToken));
});
```

The first overload wraps an existing function that needs cancellation only. The second also receives the scheduled occurrence and attempt number. `Jobs` configuration controls polling, lease duration, and retry behavior.

User Management follows the same layers:

- `UsersController` (`api/users`, `[RequirePermission(user_management)]` on the class): list, roles, get, create,
  update, `reset-password`, soft delete.
- `IUserService` / `UserService`: validation, duplicate checks, last-manager rule, soft delete (rules in
  `BusinessRules.md`).
- DTOs under `DTO/Users`, mapping in `Mapping/UserMappings.cs`, limits and default passwords in
  `Constants/UserConstants.cs`.
- Shared helpers: `Utils/Security/PasswordHasher` (the only password hashing code) and
  `Utils/Extensions/UserStatusExtensions.EnsureCanSignIn()` (used by login, the auth endpoints and `[RequirePermission]`).

Transactions follow the same layers:

- `TransactionsController` (`api/transactions`, `[RequirePermission]` per action): postings, NRC pickup/cancel,
  interbank complete/fail, list, detail and account statement. It builds a `RequestActor` (user, IP, User-Agent) with
  `HttpContext.GetRequestActor()` and reads the optional `Idempotency-Key` header.
- Services (rules in `BusinessRules.md`):
  - `ITransactionService` / `TransactionService`: deposit, withdrawal, internal transfer.
  - `IInterbankTransferService` / `InterbankTransferService`: interbank transfer and its settled / failed result.
  - `INrcTransferService` / `NrcTransferService`: NRC transfer, pickup, cancel.
  - `ITransactionQueryService` / `TransactionQueryService`: read-only list, detail, statement.
  - `LedgerPostingService` (concrete, internal to these services): database transactions with idempotency, account
    row locks, account-type debit rules, customer and GL entries, refunds, audit rows. All balance changes go through
    `PostCustomerEntryAsync`.
  - `TransactionRequestValidator`: request checks shared by the services.
- DTOs under `DTO/Transactions` (plus `DTO/Common/PagedResponse` and `RequestActor`); limits in
  `Constants/TransactionConstants.cs`, GL codes in `AccountingConstants.cs`, audit actions in `AuditConstants.cs`.
  GL accounts are seeded by `Data/Seeders/ChartOfAccountsSeeder.cs`. Example requests in `transactions.http`.
- `ApplicationDbContext` converts `DateOnly` to `DateTime` for every entity, because MySql.EntityFrameworkCore cannot
  read `DateOnly` columns back.

Branches (`Models/Organization/Branch`, seeded by `Data/Seeders/BranchSeeder` on an empty table) are the pickup
locations of NRC transfers at this bank; `GET api/transactions/branches` lists the active ones.

`AccountsController` `GET api/accounts` and `GET api/accounts/{id}` return the standard `ApiMessageResponse<T>`
envelope like every other endpoint, so the WPF `ApiClient` can read them.

## WPF transaction screens

- Sidebar: **Transactions** (permission `transactions`) and **Transaction History** (`transactions` or
  `transaction_history`, so officers and auditors both see it).
- `TransactionsViewModel` / `Views/Pages/TransactionsView` (officers): one tab per posting kind
  (`TransactionTabViewModel`, `Tab` / `Tab.Strip` styles in `Themes/Controls/Tabs.xaml`). The selected tab shows
  `TransactionFormViewModel` inline (`TransactionFormView`): one form for deposit, withdrawal, internal, interbank and
  NRC transfer, `TransactionFormKind` picks the visible fields. Opening a tab loads fresh balances; after a posting
  (`Posted`) the page shows the outcome banner and puts a fresh form in place (new idempotency key, so a retry of the
  same form never posts twice); `Clear` starts over. A new NRC transfer's pickup code is still shown once in the
  `NrcPickupCodeViewModel` dialog so it cannot be missed.
- `TransactionHistoryViewModel` / `TransactionHistoryView`: the filterable, paged table
  (`TransactionFilterViewModel` + `TransactionFilterBar`, `TransactionListViewModel` + `TransactionPager`; a newer
  load cancels an older one), the detail card and account statements. Officers (`CanManagePendingTransfers`) also get
  an Actions column on pending transfers; auditors do not (the column binds through `Behaviors/BindingProxy`).
- Dialogs (templates in `Views/DialogTemplates.xaml`):
  - `NrcPickupFormViewModel`: pickup at our branch: shows the receiver's name and NRC to check, takes the code,
    pays out in cash or into the receiver's account.
  - `PendingTransferActionViewModel`: cancel an NRC transfer, record another bank's NRC payout, or mark an interbank
    transfer settled / failed. The pending-NRC row action opens the pickup form or the payout form depending on where
    the transfer is collected.
  - `TransactionDetailsViewModel`: read-only detail card (entries, NRC or interbank detail). Each entry has a
    Statement button; `TransactionDetailsLauncher` then opens `AccountStatementViewModel`: the account's entries with
    balance after each, a date range and the shared pager.
- Services: `ITransactionService` / `TransactionService` (`api/transactions`, sends `Idempotency-Key` through
  `ApiClient.PostAsync(..., headers, ...)`) and `IAccountService` / `AccountService` (account pickers, loaded through
  `AccountOption.LoadUsableAsync`).
- Shared helpers: `Api/QueryString` (list filters, also used by `UserService`), `Utils/TransactionDisplay` (type /
  status names and MMK amounts), `ViewModels/FieldError` (per-field error for forms with many fields),
  `Constants/TransactionFieldRules` (client copy of the server limits), and the `Badge.TransactionStatus` /
  `Badge.EntryType` styles in `Views/Pages/Transactions/TransactionStyles.xaml`.

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
