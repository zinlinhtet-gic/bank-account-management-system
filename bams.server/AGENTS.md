# AGENTS.md

## Project Goal

This is a production-level ASP.NET Core / C# project.

The project follows a layered architecture:

```text
Model / Entity
      ↓
Service
      ↓
Controller
      ↓
DTO / API Response
```

Core principles:

- Controllers must remain thin.
- Business logic belongs in services.
- Database entities must not be exposed directly through API endpoints.
- Expected application failures must use custom exceptions.
- Exceptions must be handled centrally through global exception handling.
- Every user-facing or application-significant message must have a stable `MessageCode`.
- Functions must be named after what they actually do.
- Existing functions must be reused when they already provide the required behavior.
- Magic numbers and unexplained literal values are prohibited.
- Important code must be documented with comments.
- Project documentation must be updated whenever production code behavior or structure changes.

---

# 1. Core Architecture

Use the following responsibility boundaries:

```text
HTTP Request
    ↓
Controller
    ↓
Request DTO
    ↓
Service
    ↓
Model / Entity
    ↓
Database

Database
    ↓
Model / Entity
    ↓
Service
    ↓
Response DTO
    ↓
Controller
    ↓
HTTP Response
```

The controller is responsible for HTTP concerns.

The service is responsible for application and business logic.

The model/entity represents persisted application data.

DTOs represent API contracts.

---

# 2. Recommended Project Structure

```text
Project/
│
├── Controllers/
│   ├── AccountsController.cs
│   ├── CustomersController.cs
│   └── TransactionsController.cs
│
├── Models/
│   ├── Account.cs
│   ├── Customer.cs
│   └── Transaction.cs
│
├── DTOs/
│   ├── Accounts/
│   │   ├── CreateAccountRequest.cs
│   │   ├── UpdateAccountRequest.cs
│   │   ├── AccountResponse.cs
│   │   └── AccountSummaryResponse.cs
│   │
│   └── Common/
│       ├── ApiErrorResponse.cs
│       └── ApiMessageResponse.cs
│
├── Services/
│   ├── Interfaces/
│   │   ├── IAccountService.cs
│   │   └── ICustomerService.cs
│   │
│   ├── AccountService.cs
│   └── CustomerService.cs
│
├── Exceptions/
│   ├── AppException.cs
│   ├── NotFoundException.cs
│   ├── ValidationException.cs
│   ├── ConflictException.cs
│   ├── ForbiddenException.cs
│   └── BusinessRuleException.cs
│
├── Messages/
│   ├── MessageCode.cs
│   ├── MessageCatalog.cs
│   └── MessageExtensions.cs
│
├── Constants/
│   ├── AccountConstants.cs
│   ├── TransactionConstants.cs
│   └── ValidationConstants.cs
│
├── Middleware/
│   └── GlobalExceptionHandler.cs
│
├── Data/
│   ├── ApplicationDbContext.cs
│   └── Configurations/
│
├── Mapping/
│   └── AccountMappings.cs
│
├── Utils/
│   ├── Extensions/
│   └── Helpers/
│
├── Documentation/
│   ├── Architecture.md
│   ├── MessageCodes.md
│   └── BusinessRules.md
│
├── Program.cs
└── appsettings.json
```

Do not introduce additional architectural layers unless they solve an actual problem.

Avoid unnecessary Repository or UnitOfWork abstractions when Entity Framework Core already provides the required abstraction.

---

# 3. Controllers

Controllers must be thin.

Controllers may:

- Receive HTTP requests.
- Read route, query, and body parameters.
- Accept validated DTOs.
- Call services.
- Return appropriate HTTP results.
- Access HTTP-specific data such as claims when necessary.

Controllers must not:

- Query `DbContext` directly.
- Contain business rules.
- Calculate domain values.
- Perform complex validation.
- Map large object graphs manually.
- Catch application exceptions individually.
- Contain persistence logic.

Example:

```csharp
[ApiController]
[Route("api/accounts")]
public sealed class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    // Gets a single account by its unique identifier.
    [HttpGet("{id:long}")]
    public async Task<ActionResult<AccountResponse>> GetAccountByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var account = await _accountService.GetAccountByIdAsync(
            id,
            cancellationToken);

        return Ok(account);
    }
}
```

---

# 4. Services

Services contain application and business logic.

Service responsibilities include:

- Business-rule validation.
- Entity creation and modification.
- Database queries.
- Coordinating multiple entities.
- Transaction boundaries when required.
- Calling external or internal services.
- Mapping entities to DTOs.
- Throwing meaningful application exceptions.
- Selecting appropriate `MessageCode` values.

Services must not know about:

```text
HttpContext
IActionResult
ControllerBase
StatusCode()
BadRequest()
NotFound()
Ok()
```

---

# 5. Function and Method Naming Rules

Every function or method must be named according to what it actually does.

Names must communicate intent without requiring the reader to inspect the entire implementation.

Preferred:

```csharp
GetAccountByIdAsync()
CreateAccountAsync()
ValidateWithdrawalLimit()
CalculateMonthlyInterest()
CloseMaturedFixedDepositAsync()
TransferFundsAsync()
```

Avoid vague names:

```csharp
Handle()
Process()
Execute()
DoWork()
Run()
Manage()
UpdateData()
Check()
Helper()
```

Generic names are acceptable only when the surrounding abstraction already makes the intent unambiguous.

For asynchronous methods, use the `Async` suffix.

Bad:

```csharp
public Task<AccountResponse> GetAccount(long id)
```

Preferred:

```csharp
public Task<AccountResponse> GetAccountByIdAsync(long id)
```

Boolean methods should read like questions where practical.

Preferred:

```csharp
IsAccountActive()
CanWithdraw()
HasSufficientBalance()
RequiresApproval()
```

---

# 6. Reuse Existing Functions Before Creating New Ones

Before creating a new function, search the existing codebase for an implementation that already performs the same or substantially similar behavior.

Do not create duplicate methods for convenience.

Bad:

```csharp
CalculateInterest()
ComputeInterest()
GetInterestAmount()
```

when all three contain the same calculation.

Preferred:

```csharp
CalculateInterest()
```

and reuse it everywhere the same business rule applies.

Before adding a new method:

1. Search by method name.
2. Search by the business concept.
3. Search by the main types involved.
4. Inspect related services, helpers, extensions, and domain objects.
5. Reuse or extend an existing method if doing so preserves clear responsibility.

Create a new method only when:

- The behavior is genuinely different.
- The existing method would become misleading if reused.
- Reusing the method would violate separation of responsibilities.
- A separate method materially improves clarity or testability.

Do not duplicate business rules across multiple methods.

---

# 7. Method Responsibility

Methods should have one clear responsibility.

A method name and implementation must agree.

Bad:

```csharp
CreateAccountAsync()
```

that also:

- sends email,
- generates a financial report,
- updates unrelated customer data,
- performs account closure.

Split unrelated responsibilities into appropriately named methods or services.

Keep methods focused and reasonably small.

---

# 8. Constants and Magic Values

Never use unexplained magic numbers, magic strings, or business-critical literal values directly in application logic.

Bad:

```csharp
if (failedAttempts >= 5)
{
    LockAccount();
}

if (amount > 10000000)
{
    throw new BusinessRuleException(...);
}

await Task.Delay(3000);
```

Preferred:

```csharp
public static class AuthenticationConstants
{
    public const int MaximumFailedLoginAttempts = 5;
}

public static class TransactionConstants
{
    public const decimal MaximumSingleTransferAmount = 10_000_000m;
}

public static class RetryConstants
{
    public static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);
}
```

Usage:

```csharp
if (failedAttempts >= AuthenticationConstants.MaximumFailedLoginAttempts)
{
    LockAccount();
}
```

Values that must be configurable between environments should use configuration rather than compile-time constants.

Examples:

```text
API URLs
Timeouts
Feature flags
Environment-specific limits
Connection strings
External credentials
```

Business invariants that are fixed by the system may use constants.

---

# 9. Constant Naming

Constants must describe what the value means.

Bad:

```csharp
const int Limit = 5;
const decimal Max = 10000000m;
const int Time = 30;
```

Preferred:

```csharp
const int MaximumFailedLoginAttempts = 5;
const decimal MaximumDailyWithdrawalAmount = 10_000_000m;
const int AccountLockDurationMinutes = 30;
```

Do not name constants after their raw value.

Bad:

```csharp
const int Five = 5;
```

---

# 10. Comments Are Required

Comments are mandatory for production code when they improve understanding of intent, business rules, or non-obvious behavior.

Every public or significant private function must have a comment immediately before it describing what it does.

Example:

```csharp
// Calculates monthly interest using the account's configured annual interest rate.
private decimal CalculateMonthlyInterest(
    decimal balance,
    decimal annualRate)
{
    return balance * annualRate / InterestConstants.MonthsPerYear;
}
```

Important code phrases or logical sections must have a short comment before them.

Example:

```csharp
// Reject withdrawals when the account is currently frozen.
if (account.Status == AccountStatus.Frozen)
{
    throw new BusinessRuleException(
        MessageCode.AccountFrozen);
}

// Calculate the available balance after reserving pending transactions.
var availableBalance =
    account.Balance - pendingTransactionAmount;
```

Comments must explain:

- Why the code exists.
- What business rule is being enforced.
- Why a non-obvious implementation was chosen.
- Why an edge case must be handled.
- Why a workaround exists.

Comments should not merely restate obvious syntax.

Bad:

```csharp
// Increment i by 1.
i++;
```

Preferred:

```csharp
// Advance to the next repayment period after the current installment is processed.
period++;
```

---

# 11. XML Documentation

Public APIs, reusable services, shared utilities, and important domain methods should use XML documentation where useful.

Example:

```csharp
/// <summary>
/// Calculates the interest payable for the specified fixed-deposit account.
/// </summary>
/// <param name="account">The fixed-deposit account.</param>
/// <param name="calculationDate">The date used for the interest calculation.</param>
/// <returns>The calculated interest amount.</returns>
public decimal CalculateFixedDepositInterest(
    FixedDepositAccount account,
    DateOnly calculationDate)
{
    // Apply the rate associated with the account's current deposit term.
    return ...;
}
```

Use normal `//` comments for local implementation intent.

Use `///` XML comments for reusable or externally consumed APIs.

---

# 12. DTO Rules

Use separate DTOs for separate API operations.

Examples:

```text
CreateAccountRequest
UpdateAccountRequest
AccountResponse
AccountSummaryResponse
```

Request DTOs describe input.

Response DTOs describe output.

Entities describe stored application data.

Entities must not be returned directly from API endpoints.

---

# 13. DTO Mapping

Keep mapping explicit unless complexity justifies a mapping library.

Example:

```csharp
public static class AccountMappings
{
    // Converts an Account entity into the API response contract.
    public static AccountResponse ToResponse(this Account account)
    {
        return new AccountResponse(
            account.Id,
            account.AccountNumber,
            account.Name,
            account.Type,
            account.Balance);
    }
}
```

---

# 14. Message Code Architecture

Every meaningful application message must have a stable message code.

A message code represents the semantic meaning of the event.

Message text may change.

Message codes must remain stable.

Clients must rely on the code rather than parsing message text.

Bad:

```csharp
if (response.Message == "Account not found")
{
}
```

Preferred:

```csharp
if (response.Code == MessageCode.AccountNotFound)
{
}
```

---

# 15. Message Categories

Recommended ranges:

```text
1000 - 1999   Success
2000 - 2999   Information
3000 - 3999   Validation
4000 - 4099   Authentication
4100 - 4199   Authorization
4200 - 4299   Not Found
4300 - 4399   Conflict
4400 - 4499   Business Rule
5000 - 5999   System / Infrastructure
```

Once a code is released to clients, do not reuse or renumber it without an explicit migration strategy.

---

# 16. MessageCode Enum

```csharp
public enum MessageCode
{
    // Success: 1000 - 1999
    Success = 1000,
    CreatedSuccessfully = 1001,
    UpdatedSuccessfully = 1002,
    DeletedSuccessfully = 1003,
    AccountCreatedSuccessfully = 1100,
    AccountUpdatedSuccessfully = 1101,
    AccountClosedSuccessfully = 1102,
    TransactionCompletedSuccessfully = 1200,
    TransferCompletedSuccessfully = 1201,

    // Information: 2000 - 2999
    Information = 2000,
    NoChangesDetected = 2001,
    ProcessingStarted = 2002,
    ProcessingCompleted = 2003,
    AccountAlreadyActive = 2100,
    AccountPendingApproval = 2101,

    // Validation: 3000 - 3999
    ValidationFailed = 3000,
    RequiredFieldMissing = 3001,
    InvalidRequest = 3002,
    InvalidAmount = 3003,
    InvalidDate = 3004,
    AccountNameRequired = 3100,
    AccountTypeInvalid = 3101,
    OpeningBalanceInvalid = 3102,
    TransferAmountInvalid = 3200,
    SourceAccountRequired = 3201,
    DestinationAccountRequired = 3202,

    // Authentication: 4000 - 4099
    AuthenticationRequired = 4000,
    InvalidCredentials = 4001,
    AuthenticationTokenExpired = 4002,
    AuthenticationTokenInvalid = 4003,

    // Authorization: 4100 - 4199
    AccessDenied = 4100,
    InsufficientPermission = 4101,
    OperationNotPermitted = 4102,

    // Not Found: 4200 - 4299
    ResourceNotFound = 4200,
    AccountNotFound = 4201,
    CustomerNotFound = 4202,
    TransactionNotFound = 4203,

    // Conflict: 4300 - 4399
    Conflict = 4300,
    DuplicateResource = 4301,
    AccountAlreadyExists = 4302,
    DuplicateAccountNumber = 4303,
    ConcurrentModification = 4304,

    // Business Rules: 4400 - 4499
    BusinessRuleViolation = 4400,
    InsufficientBalance = 4401,
    AccountClosed = 4402,
    AccountFrozen = 4403,
    WithdrawalNotAllowed = 4404,
    TransferNotAllowed = 4405,
    DepositNotAllowed = 4406,
    AccountNotMatured = 4410,
    WithdrawalLimitExceeded = 4411,
    TransactionLimitExceeded = 4412,

    // System / Infrastructure: 5000 - 5999
    InternalServerError = 5000,
    DatabaseError = 5001,
    ExternalServiceUnavailable = 5002,
    TimeoutOccurred = 5003,
    ServiceUnavailable = 5004
}
```

---

# 17. Message Catalog

Message text should be resolved centrally.

```csharp
public static class MessageCatalog
{
    private static readonly IReadOnlyDictionary<MessageCode, string> Messages =
        new Dictionary<MessageCode, string>
        {
            [MessageCode.Success] =
                "Operation completed successfully.",

            [MessageCode.AccountCreatedSuccessfully] =
                "Account created successfully.",

            [MessageCode.AccountNotFound] =
                "Account was not found.",

            [MessageCode.ValidationFailed] =
                "One or more validation errors occurred.",

            [MessageCode.InvalidAmount] =
                "The provided amount is invalid.",

            [MessageCode.InsufficientBalance] =
                "The account has insufficient balance.",

            [MessageCode.AccountClosed] =
                "The account is closed.",

            [MessageCode.AccountFrozen] =
                "The account is frozen.",

            [MessageCode.TransferNotAllowed] =
                "Transfer is not allowed for this account.",

            [MessageCode.InternalServerError] =
                "An unexpected error occurred."
        };

    // Resolves the human-readable message associated with a message code.
    public static string GetMessage(MessageCode code)
    {
        return Messages.TryGetValue(code, out var message)
            ? message
            : "Unknown application message.";
    }
}
```

Do not scatter message strings throughout services and controllers.

---

# 18. Base Application Exception

Expected application exceptions must derive from a common base exception.

```csharp
public abstract class AppException : Exception
{
    public MessageCode Code { get; }

    // Creates an application exception using the standard message for the code.
    protected AppException(MessageCode code)
        : base(MessageCatalog.GetMessage(code))
    {
        Code = code;
    }

    // Creates an application exception using a contextual message while preserving the stable code.
    protected AppException(
        MessageCode code,
        string message)
        : base(message)
    {
        Code = code;
    }
}
```

---

# 19. Custom Exceptions

Recommended hierarchy:

```text
Exception
    ↓
AppException
    ├── NotFoundException
    ├── ValidationException
    ├── ConflictException
    ├── ForbiddenException
    └── BusinessRuleException
```

Example:

```csharp
public sealed class BusinessRuleException : AppException
{
    // Creates a business-rule exception associated with a stable application message code.
    public BusinessRuleException(MessageCode code)
        : base(code)
    {
    }
}
```

Usage:

```csharp
// Prevent a withdrawal that would exceed the account's available balance.
if (account.Balance < amount)
{
    throw new BusinessRuleException(
        MessageCode.InsufficientBalance);
}
```

---

# 20. Global Exception Handling

Controllers must not repeatedly contain local try/catch blocks for expected application failures.

Recommended mapping:

```text
NotFoundException      → 404
ValidationException    → 400
ConflictException      → 409
ForbiddenException     → 403
BusinessRuleException  → 422
Unexpected Exception   → 500
```

All unexpected exceptions must be logged server-side.

Do not expose stack traces, SQL messages, connection strings, internal paths, or infrastructure details to clients.

---

# 21. API Error Response

Use a predictable structure.

```csharp
public sealed record ApiErrorResponse(
    int Code,
    string Name,
    string Message,
    string? TraceId = null);
```

Example:

```json
{
  "code": 4201,
  "name": "AccountNotFound",
  "message": "Account was not found.",
  "traceId": "00-b942..."
}
```

---

# 22. HTTP Status vs MessageCode

HTTP status and `MessageCode` serve different purposes.

Example:

```text
HTTP Status: 404
Message Code: 4201
Message Name: AccountNotFound
```

HTTP status describes the protocol-level result.

`MessageCode` describes the specific application-level result.

---

# 23. Validation

Separate request validation from business validation.

Request validation examples:

```text
Required field
Maximum length
Invalid enum
Malformed value
Invalid date
```

Business validation examples:

```text
Account is closed
Account is frozen
Insufficient balance
Transaction exceeds limit
Account has not matured
Transfer is not permitted
```

Every validation failure must use an appropriate `MessageCode`.

---

# 24. Entity Framework Core

Use EF Core directly inside services unless a repository abstraction has a clear project-specific justification.

For read-only queries prefer:

```csharp
.AsNoTracking()
```

Project only the columns required by the response whenever practical.

Avoid unnecessary database round trips.

Avoid loading entire tables into memory before filtering.

Avoid N+1 query patterns.

---

# 25. Async Rules

Database, network, filesystem, gRPC, and HTTP operations should be asynchronous.

Use:

```csharp
async Task<T>
```

Do not use:

```csharp
.Result
.Wait()
.GetAwaiter().GetResult()
```

unless there is an exceptional and documented reason.

Propagate `CancellationToken` through:

```text
Controller
    ↓
Service
    ↓
EF Core / gRPC / HTTP Client
```

---

# 26. Dependency Injection

Use constructor injection.

Example:

```csharp
builder.Services.AddScoped<IAccountService, AccountService>();
```

Avoid service locator patterns and manually constructing services when dependency injection should manage them.

---

# 27. Logging

Use structured logging.

Preferred:

```csharp
_logger.LogInformation(
    "Account {AccountId} created for customer {CustomerId}",
    account.Id,
    customer.Id);
```

Never log:

- Passwords.
- Authentication tokens.
- PINs.
- API secrets.
- Full card numbers.
- Connection strings.
- Sensitive personal information.

Expected business-rule failures should generally not be logged as application errors.

---

# 28. Database Transactions

Use explicit transactions only when multiple operations must succeed or fail atomically and normal `SaveChanges` behavior is insufficient.

Never silently swallow exceptions.

Rollback when appropriate, then rethrow the original exception.

---

# 29. Naming Conventions

Use standard C# naming:

```text
Class             PascalCase
Method            PascalCase
Property          PascalCase
Public member     PascalCase
Private field     _camelCase
Parameter         camelCase
Local variable    camelCase
Interface         IPascalCase
Async method      MethodNameAsync
Enum              PascalCase
Enum member       PascalCase
Constant          PascalCase
```

---

# 30. Nullable Reference Types

Keep nullable reference types enabled.

Do not suppress nullability warnings without understanding why.

Handle null conditions explicitly.

---

# 31. Date and Time

Persist server-side timestamps in UTC.

Prefer:

```csharp
DateTime.UtcNow
```

or an injected `TimeProvider` for testable time-dependent logic.

---

# 32. Money

Financial values must use:

```csharp
decimal
```

Never use:

```csharp
float
double
```

for monetary calculations.

Financial rounding rules must be explicit.

Rounding modes, decimal precision, and scale must not be hidden magic values.

---

# 33. Security

Never trust client input.

Authorization must be enforced server-side.

Do not rely on:

```text
Hidden UI controls
Disabled buttons
Frontend validation
Client-provided user IDs
```

as security mechanisms.

Do not expose sensitive information through exception or validation messages.

---

# 34. Testing Expectations

Prioritize tests for:

```text
Business rules
Financial calculations
Validation
Permission rules
State transitions
Message codes
Exception types
Failure scenarios
Service behavior
```

Tests should primarily verify `MessageCode` rather than exact English wording.

Also test that:

- Message-code values are unique.
- Required message catalog entries exist.
- Constants are used for defined business limits.
- Duplicate business logic is not introduced where practical to detect.
- Global exception handling returns the correct code and HTTP status.

---

# 35. Documentation Update Rule

Documentation is part of the code change.

Whenever production code is created, modified, renamed, moved, or removed, review the related documentation and update it in the same change when behavior, architecture, public contracts, business rules, or usage have changed.

Do not postpone documentation changes.

Examples that require documentation updates:

```text
New service method
Renamed method
Changed business rule
New MessageCode
Changed MessageCode meaning
New exception type
Changed API contract
New DTO
Changed DTO property
Changed constant or configured limit
Changed transaction flow
New dependency
Changed architecture
Changed directory structure
Changed configuration requirement
Changed integration behavior
```

Examples of documentation files:

```text
README.md
AGENTS.md
Documentation/Architecture.md
Documentation/MessageCodes.md
Documentation/BusinessRules.md
API documentation
XML documentation
```

If a code change does not affect documented behavior, no artificial documentation change is required.

However, the documentation impact must still be considered.

---

# 36. Code Change Workflow for Agents

Before changing code:

1. Read the relevant implementation.
2. Search for existing functions that already perform the required behavior.
3. Search for existing constants.
4. Search for existing `MessageCode` values.
5. Search for existing DTOs, exceptions, helpers, and services.
6. Read the related documentation.

While changing code:

1. Reuse existing functions when appropriate.
2. Name new functions according to their actual responsibility.
3. Do not introduce magic values.
4. Add or reuse named constants.
5. Add meaningful comments before significant functions and code sections.
6. Use existing `MessageCode` values when their meaning already matches.
7. Add a new message code only for a genuinely new semantic condition.
8. Keep controllers thin.
9. Keep business logic in services.
10. Preserve established architectural boundaries.

After changing code:

1. Check for duplicate functions.
2. Check for duplicated business rules.
3. Check for magic numbers and unexplained literal values.
4. Check comments and XML documentation.
5. Check `MessageCode` consistency.
6. Check exception handling.
7. Check tests.
8. Update related documentation.
9. Ensure renamed or removed APIs are also updated in documentation.
10. Ensure examples in documentation still compile conceptually and match the current architecture.

A code change is not complete until its required documentation changes are also complete.

---

# 37. Code Generation Rules for Agents

When generating or modifying code:

1. Follow the existing project architecture.
2. Keep controllers thin.
3. Put business logic in services.
4. Do not expose EF Core entities directly.
5. Use request and response DTOs.
6. Use custom exceptions for expected failures.
7. Every expected application exception must contain a `MessageCode`.
8. Every user-facing application message must have a `MessageCode`.
9. Do not scatter message strings throughout services or controllers.
10. Never reuse an existing message-code number for a different meaning.
11. Search for an existing function before creating a new one.
12. Reuse an existing function when it already provides the required behavior.
13. Do not create duplicate business logic.
14. Name functions according to what they actually do.
15. Use the `Async` suffix for asynchronous methods.
16. Do not use vague function names such as `Handle`, `Process`, or `DoWork` unless context makes the meaning explicit.
17. Do not use magic numbers.
18. Do not use unexplained business-critical magic strings.
19. Define named constants for fixed invariant values.
20. Use configuration for environment-specific values.
21. Add comments before significant functions.
22. Add comments before important or non-obvious code sections.
23. Comments must explain intent or business reasoning rather than repeat syntax.
24. Use XML documentation for important public or reusable APIs.
25. Global exception handling must convert exceptions into HTTP responses.
26. Services must not return HTTP-specific results.
27. Pass `CancellationToken`.
28. Use `decimal` for monetary values.
29. Use UTC for persisted timestamps.
30. Prefer `AsNoTracking()` for read-only EF Core queries.
31. Avoid unnecessary database round trips.
32. Avoid unnecessary abstractions.
33. Do not introduce repositories unless justified.
34. Never swallow exceptions.
35. Do not expose internal exception details.
36. Use dependency injection.
37. Use structured logging.
38. Log message codes when useful.
39. Keep methods focused.
40. Follow existing naming conventions.
41. Preserve backward compatibility of released message codes.
42. Update related documentation in the same change.
43. Treat documentation updates as part of the definition of done.

---

# 38. Architectural Rule of Thumb

```text
HTTP-specific?
    → Controller

API input/output contract?
    → DTO

Business/application logic?
    → Service

Persisted application data?
    → Model / Entity

Expected application failure?
    → Custom Exception

Specific application result/failure meaning?
    → MessageCode

Human-readable message?
    → MessageCatalog

Fixed business/system invariant?
    → Named Constant

Environment-dependent value?
    → Configuration

Exception → HTTP conversion?
    → Global Exception Handler
```

---

# 39. Message Architecture Rule of Thumb

```text
MessageCode
     ↓
MessageCatalog
     ↓
AppException
     ↓
Specific Exception
     ↓
Global Exception Handler
     ↓
ApiErrorResponse
```

Clients should use stable message codes for behavior decisions instead of parsing human-readable text.

---

# 40. Definition of Done

A production code change is complete only when all applicable items are satisfied:

- Code follows the existing architecture.
- No duplicate function was created unnecessarily.
- Functions are named according to their behavior.
- No unexplained magic numbers remain.
- Constants or configuration are used appropriately.
- Business rules are not duplicated.
- Required comments are present.
- Public reusable APIs have useful documentation.
- Message codes are correct and stable.
- Exceptions use the correct custom type.
- Global exception handling remains consistent.
- Tests cover the changed behavior.
- Relevant documentation has been updated.
- Documentation examples remain consistent with the implementation.
