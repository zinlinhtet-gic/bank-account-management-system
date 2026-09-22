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

The customer vertical currently supports list, get-by-id, and creation:

- `CustomersController` for thin customer API endpoints:
  - `GET /api/customers` — all customer summaries.
  - `GET /api/customers/{id}` (named route `GetCustomerById`) — a single customer with its documents.
  - `POST /api/customers` — creates a customer; accepts `[FromForm]` so the request can carry uploaded document files alongside customer fields (multipart form data). Returns `201 Created` via `CreatedAtRoute` pointing at `GetCustomerById`.
- `ICustomerService` and `CustomerService` for application behavior. `GetCustomersAsync`/`GetCustomerByIdAsync` are simple `AsNoTracking` reads (the list projects directly to `CustomerSummaryResponse` so EF Core can translate the query to SQL; the detail query `Include`s `Documents`). `CreateCustomerAsync` is a thin orchestrator that delegates to focused private steps: `ValidateRequest`, `EnsureCustomerIsUniqueAsync`, `BuildCustomer`, `AttachCustomerDocumentsAsync`, and (on failure) `DeleteSavedFilesAsync`.
- `CustomerNumberGenerator` for generating the next sequential customer number.
- `IFileStorageService` / `LocalFileStorageService` for saving and deleting uploaded document files on local disk (under `wwwroot`-adjacent `uploads/<folder>`), independent of business logic in `CustomerService`.
- `Customer` and `CustomerDocument` as the EF Core entities (one customer has many documents).
- Customer, customer-summary, and customer-document request/response DTOs under `DTO/Customers`.
- `CustomerConstants` for customer business invariants (name length, minimum age, number prefix, email format, document upload folder).

Creating a customer with documents is a single transactional operation: `CustomerService` opens a database transaction, saves any uploaded files first, adds the customer (with its documents attached via the `Customer.Documents` navigation), and commits. If saving to the database fails, the transaction rolls back and any files already written are deleted, so a failed request never leaves orphaned uploads behind.
