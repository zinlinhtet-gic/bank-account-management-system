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

The customer vertical currently supports list, get-by-id, creation, partial update, and KYC review:

- `CustomersController` for thin customer API endpoints:
  - `GET /api/customers` — a page of customer summaries (`[FromQuery] GetCustomersRequest`: `PageNumber` plus optional `CustomerNo`, `CustomerName`, `KycStatus`, `Status`, `RiskLevel`, `StartDate`/`EndDate` filters), returned as `PagedResponse<CustomerSummaryResponse>`. Page size is fixed by `CustomerConstants.CustomersPageSize` (10), not caller-configurable. `StartDate`/`EndDate` filter by the customer's `CreatedAt` date, inclusive on both ends.
  - `GET /api/customers/{id}` (named route `GetCustomerById`) — a single customer with its documents.
  - `POST /api/customers` — creates a customer; accepts `[FromForm]` so the request can carry uploaded document files alongside customer fields (multipart form data). Returns `201 Created` via `CreatedAtRoute` pointing at `GetCustomerById`.
  - `PATCH /api/customers/{id}` — partially updates a customer; accepts `[FromForm] UpdateCustomerRequest` (multipart form data, like create) so a document being added or edited can carry an uploaded file. Every customer field is optional: a supplied (non-null) value replaces the existing one, an omitted/null value leaves it unchanged. PUT was intentionally not used, since callers only ever send the subset of fields they want to change, not the full resource. `Documents` entries with an `Id` edit an existing document (only its supplied fields change, and a supplied `File` replaces its stored file); entries without an `Id` add a new document (requires `DocumentType`).
  - `POST /api/customers/{id}/kyc-review` — records a KYC review decision (`[FromBody] ReviewCustomerKycRequest`: `KycStatus` and `ReviewedByUserId`), a distinct action endpoint rather than a field patch since it's a one-way workflow decision, not an arbitrary field edit. `ReviewedByUserId` must belong to a user holding the `RoleConstants.Manager` role, or the request is rejected with `ForbiddenException(MessageCode.AccessDenied)`.
- `ICustomerService` and `CustomerService` for application behavior. `GetCustomersAsync` builds an `AsNoTracking` query via `BuildCustomerFilterQuery` (applies only the filters actually supplied, each as its own `Where`), counts the filtered total for `PagedResponse.TotalPages`, then pages with `Skip`/`Take` and projects directly to `CustomerSummaryResponse` so EF Core can translate the whole query to SQL. `GetCustomerByIdAsync` is a similar `AsNoTracking` read that `Include`s `Documents`. `CreateCustomerAsync` and `UpdateCustomerAsync` share the same field-validation (`ValidateCustomerFields`) and duplicate-check (`EnsureCustomerIsUniqueAsync`, with an `excludeCustomerId` so update doesn't conflict with itself) logic rather than duplicating the business rules — `CreateCustomerAsync` validates the request fields directly, `UpdateCustomerAsync` first merges the request over the existing customer's values, then validates the merged result. Both are thin orchestrators: `CreateCustomerAsync` delegates to `BuildCustomer` and `AttachCustomerDocumentsAsync` (documents can only be added); `UpdateCustomerAsync` delegates to `ValidateDocumentChangeRequests` (checked before any file I/O — every `Id` must reference a document the customer already has, and a new document must specify a type) and `ApplyCustomerDocumentChangesAsync` (adds/edits documents and saves files). Both wrap their document/file work plus `SaveChangesAsync` in a database transaction and call `DeleteSavedFilesAsync` to clean up: newly saved files on rollback, or a replaced document's old file only after a successful commit. `ReviewCustomerKycAsync` needs no new storage: it reuses the existing per-document `VerifiedAt`/`VerifiedBy` fields on `CustomerDocument` (there is no separate "KYC verified by/at" on `Customer` itself) — approving stamps those fields on every document the customer currently has, rejecting only changes `Customer.KycStatus`.
- `CustomerNumberGenerator` for generating the next sequential customer number.
- `IFileStorageService` / `LocalFileStorageService` for saving and deleting uploaded document files on local disk (under `wwwroot`-adjacent `uploads/<folder>`), independent of business logic in `CustomerService`.
- `Customer` and `CustomerDocument` as the EF Core entities (one customer has many documents).
- Customer, customer-summary, customer-list-query, customer-update, customer-kyc-review, and customer-document request/response DTOs under `DTO/Customers`; the generic `PagedResponse<T>` lives under `DTO/Common` since it isn't customer-specific.
- `CustomerConstants` for customer business invariants (name length, minimum age, number prefix, email format, document upload folder, page size).

Creating a customer with documents is a single transactional operation: `CustomerService` opens a database transaction, saves any uploaded files first, adds the customer (with its documents attached via the `Customer.Documents` navigation), and commits. If saving to the database fails, the transaction rolls back and any files already written are deleted, so a failed request never leaves orphaned uploads behind.

`ApplicationDbContext.ConfigureConventions` registers a global `DateOnly <-> DateTime` value converter (keeping the MySQL column type as `date`) for every `DateOnly`/`DateOnly?` property in the model. This works around a bug in the `MySql.EntityFrameworkCore` provider (v10.0.9) whose reader throws `InvalidCastException` trying to materialize a `date` column directly as `DateOnly` — it affects any entity with a `DateOnly` property (e.g. `Customer.DateOfBirth`, `CustomerDocument.IssuedDate`/`ExpiryDate`, `FixedDeposit` term dates), not just customers.

## Security reference data

- `RoleConstants` defines the three stable role codes (`Manager`, `Officer`, `Auditor`) used by code that needs to check a user's role (e.g. `CustomerService.ReviewCustomerKycAsync` requiring `Manager`).
- `Data/Seeders/SecuritySeeder.cs` seeds those three `Role` rows and one example `User` per role (with their `UserRole` assignment) — only in `IsDevelopment()` (wired up in `Program.cs`), and only if `Roles`/`Users` are empty, so it never runs against, or overwrites, a real environment. Seeded users get a placeholder, non-functional `PasswordHash` since authentication isn't implemented yet.
