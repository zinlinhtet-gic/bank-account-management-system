# Architecture

This project follows a layered ASP.NET Core structure:

HTTP request -> Controller -> DTO -> Service -> Model/Entity -> Database.

Controllers only handle HTTP routing and response concerns. Services contain business logic, validation, persistence coordination, and DTO mapping.

The account API includes:

- `AccountsController` for thin account API endpoints.
- `AccountTypesController` for the read-only available-product catalog endpoint.
- `IAccountService` and `AccountService` for application behavior.
- `IAccountHolderService` and `AccountHolderService` for holder resolution, validation, creation, and joint-holder updates.
- `IAccountTypeService` and `AccountTypeService` for account-type lookup, opening-balance validation, and product classification.
- `IFixedDepositService` and `FixedDepositService` for fixed-deposit creation, updates, payout validation, maturity, and renewal.
- `Account` as the EF Core entity.
- Account request and response DTOs under `DTO/Accounts`.
- Central message, exception, and middleware infrastructure.

<<<<<<< HEAD
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
`AccountsController` exposes account queries and creation together with status, balance-adjustment, joint-holder, and fixed-deposit update endpoints. It resolves the acting user from the standard name-identifier claim and currently uses a documented development fallback until authentication is enabled.
=======
`AccountsController` exposes account queries and creation together with status, balance-adjustment, joint-holder, and fixed-deposit update endpoints. Account mutation endpoints require the account-management permission and do not accept actor identifiers from clients.
>>>>>>> 7b9597b (fix [server] : Add get current login user and use that for account related and other operations)

Account creation accepts multipart form data. `AccountDocumentService` validates account-type document requirements, delegates private file handling to `FileUploadUtils`, and persists `AccountDocument` metadata. Files are stored outside `wwwroot`; only relative generated references are stored in the database.

Account type document requirements are normalized through `AccountTypeRequiredDocument`, allowing required document lists to change without adding product-specific columns.

`AccountService` remains the account-operation facade and delegates holder-specific rules and persistence to `AccountHolderService`.
It delegates account-type lookup and account-type-specific validation to `AccountTypeService`.

Account listing uses forward-only keyset pagination over the immutable account identifier. The service applies account-number search, account-type, and status filters before reading one more row than the requested page size to determine whether a continuation cursor is available without running a total-count query.

`CurrentUserService` resolves the authenticated user ID from the standard JWT name-identifier claim and rejects missing or invalid identities. `AuditLogService` uses that trusted identity while tracking audit entities without committing independently, allowing the owning business operation to persist its data and audit record in the same transaction. Account status history uses the same current-user abstraction for `ChangedBy` attribution.
<<<<<<< HEAD
`AuditLogService` tracks audit entities without committing independently, allowing the owning business operation to persist its data and audit record in the same transaction.
=======
>>>>>>> 7b9597b (fix [server] : Add get current login user and use that for account related and other operations)

Database migrations run during application startup, followed by the idempotent `ProductSeeder`. EF configurations contain schema mapping only; product and required-document population belongs under `Data/Seeders`.

The EF model applies global `DateOnly` value converters because the MySQL provider materializes `DATE` values as `DateTime`. Both nullable and non-nullable date properties remain stored as database `date` columns.

In the Development environment, the idempotent `TestDataSeeder` runs after product seeding and creates two verified test customers with three individual accounts each. Test data is never inserted during non-development startup.
