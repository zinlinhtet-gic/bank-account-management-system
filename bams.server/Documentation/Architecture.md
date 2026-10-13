# Architecture

## Request flow

```text
HTTP request → controller → request DTO → service → EF Core → MySQL
HTTP response ← response DTO ← service mapping ← EF Core ← MySQL
```

Controllers own HTTP concerns: routing, model binding, authorization, and response status/envelopes. Services coordinate validation, business rules, persistence, and mapping. Entities are persistence models and are not returned directly by API endpoints. Expected application failures use the application exception types and are translated centrally by `GlobalExceptionHandler`.

## Project boundaries

| Area | Responsibility |
| --- | --- |
| `Controllers/` | HTTP endpoints and permission declarations. |
| `DTO/` | Request and response contracts, grouped by feature. |
| `Services/` | Application workflows and domain rules; interfaces are colocated by feature. |
| `Models/` | Persisted entities and domain state. |
| `Data/` | `ApplicationDbContext`, EF Core configuration, migrations, and seeders. |
| `Mapping/` | Explicit entity-to-contract mapping. |
| `Messages/` and `Exceptions/` | Stable error/success codes and expected application failures. |
| `Middleware/` | Central exception conversion and HTTP response handling. |
| `Jobs/` and `Configuration/` | Scheduled execution and its schedule/options types. |

The account, customer, user, transaction, accounting, cash-operation, and business-date features follow these boundaries. Feature services can delegate focused work to other services; controllers should not take over service responsibilities.

## Persistence and consistency

The server uses EF Core with MySQL. Startup applies database migrations. `ApplicationDbContext` configures a global `DateOnly` conversion so dates remain MySQL `date` values and can be materialized by the configured provider.

Account, account-holder, and fixed-deposit records use application-managed optimistic concurrency versions. Callers send the version they read; stale updates fail with HTTP 409 and `ConcurrentModification`. Operations that change business state and audit history together use one database transaction. Audit records are committed by the owning workflow rather than independently.

Uploaded customer and account documents use `IFileStorageService` and configured private storage. Services coordinate file cleanup with database transaction outcomes so failed writes do not leave newly uploaded files orphaned. No public file URL should be inferred from a stored reference.

## Authentication and authorization

JWT authentication identifies the current user. `CurrentUserService` reads the user ID from the name-identifier claim, and services use that identity for audit attribution; clients cannot choose the actor for ordinary requests. `[RequirePermission]` checks permissions granted through roles. Authentication, disabled-user, and permission failures are represented by stable API error codes.

Development-only security seed data is registered through `Program.cs`; it is not production authentication setup. Check the current seeder and environment guard before relying on seeded users or roles.

## Background jobs

`JobsHostedService` polls configured schedules and delegates each occurrence through `JobsOperationService` using scoped services. Definitions and execution attempts are persisted in `ScheduledJobs` and `ScheduledJobExecutions`. Database leases coordinate multiple server instances, and attempt records retain outcome and failure details. Retry behavior and polling/lease settings come from the `Jobs` configuration.

Register scheduled work in `Program.cs` through `AddScheduledJobs`. Prefer an existing service method as the handler. Financial posting jobs coordinate transaction, ledger, account-transaction, and audit writes so a posting is atomic. Business-date close is a separately authorized workflow; scheduled work does not bypass its gates.

## Desktop client boundary

The WPF application calls the server through its API client. Successful endpoints use `ApiMessageResponse<T>`; expected errors use `ApiErrorResponse`. The desktop should branch on numeric message codes, not localized message text. Server changes to routes, DTOs, permissions, or message codes may require matching desktop updates.

## Keeping this page current

Use the source tree as the authority for current class names and behavior. Update this overview when a project boundary, persistence strategy, security flow, or job lifecycle changes. Endpoint details belong in [API Contracts](ApiContracts.md); domain invariants belong in [Business Rules](BusinessRules.md).
