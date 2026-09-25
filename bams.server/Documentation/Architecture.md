# Architecture

This project follows a layered ASP.NET Core structure:

HTTP request -> Controller -> DTO -> Service -> Model/Entity -> Database.

Controllers only handle HTTP routing and response concerns. Services contain business logic, validation, persistence coordination, and DTO mapping.

The initial account template includes:

- `AccountsController` for thin account API endpoints.
- `IAccountService` and `AccountService` for application behavior.
- `Account` as the EF Core entity.
- Account request and response DTOs under `DTO/Accounts`.
- Central message, exception, and middleware infrastructure.

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

All auth failures use the standard `ApiErrorResponse` body:

- `[RequirePermission]` (`Middlewares/MiddlewareAttribute.cs`) throws `UnauthorizedException` (401) for a missing
  or unknown user, and `ForbiddenException` (403) for a disabled user or missing permission. `GlobalExceptionHandler`
  turns these into responses.
- `[Authorize]` challenges are written by the `JwtBearerEvents.OnChallenge` handler in `Program.cs`.
- Controllers read the caller's id with `User.GetRequiredUserId()` (`Utils/Extensions/ClaimsPrincipalExtensions.cs`).

## WPF client error flow

`bams.desktop/Api/ApiClient.cs` is the single place that sends HTTP requests. It unwraps `ApiMessageResponse<T>.Data`,
turns `ApiErrorResponse` into `ApiException` (keeping the server `Code`, `Message` and `TraceId`), and turns transport
failures into `NetworkException`. Both derive from `AppException`, so ViewModels catch `AppException` and decide by
`exception.Code`, never by message text.
