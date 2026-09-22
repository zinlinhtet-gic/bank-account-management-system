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

`AccountsController` exposes account queries and creation together with status, balance-adjustment, joint-holder, and fixed-deposit update endpoints. It resolves the acting user from the standard name-identifier claim and currently uses a documented development fallback until authentication is enabled.

Account creation accepts multipart form data. `AccountDocumentService` validates account-type document requirements, delegates private file handling to `FileUploadUtils`, and persists `AccountDocument` metadata. Files are stored outside `wwwroot`; only relative generated references are stored in the database.

Account type document requirements are normalized through `AccountTypeRequiredDocument`, allowing required document lists to change without adding product-specific columns.

`AccountService` remains the account-operation facade and delegates holder-specific rules and persistence to `AccountHolderService`.
It delegates account-type lookup and account-type-specific validation to `AccountTypeService`.

`AuditLogService` tracks audit entities without committing independently, allowing the owning business operation to persist its data and audit record in the same transaction.

Database migrations run during application startup, followed by the idempotent `ProductSeeder`. EF configurations contain schema mapping only; product and required-document population belongs under `Data/Seeders`.

The EF model applies global `DateOnly` value converters because the MySQL provider materializes `DATE` values as `DateTime`. Both nullable and non-nullable date properties remain stored as database `date` columns.

In the Development environment, the idempotent `TestDataSeeder` runs after product seeding and creates two verified test customers with three individual accounts each. Test data is never inserted during non-development startup.
