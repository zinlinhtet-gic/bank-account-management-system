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
Account creation accepts multipart form data. `AccountDocumentService` validates account-type document requirements, delegates private file handling to `FileUploadUtils`, and persists `AccountDocument` metadata. Files are stored outside `wwwroot`; only relative generated references are stored in the database.

Account type document requirements are normalized through `AccountTypeRequiredDocument`, allowing required document lists to change without adding product-specific columns.

Database migrations run during application startup, followed by the idempotent `ProductSeeder`. EF configurations contain schema mapping only; product and required-document population belongs under `Data/Seeders`.
