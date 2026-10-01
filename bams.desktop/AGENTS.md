# AGENTS-WPF.md

## Project Goal

This is a production-level WPF client application built with MVVM.

The application communicates with a server through a dedicated Service layer.

The project follows this responsibility flow:

```text
View
  ↓
ViewModel
  ↓
Service
  ↓
HTTP / API
  ↓
Server
```

Data returned from the server flows back as:

```text
Server
  ↓
API Response DTO
  ↓
Service
  ↓
ViewModel
  ↓
View
```

Core principles:

- Views must remain thin.
- Code-behind must contain only view-specific behavior that cannot reasonably live in a ViewModel.
- ViewModels contain presentation state and presentation logic.
- ViewModels must not call `HttpClient` directly.
- API communication belongs in Services.
- DTOs define client/server contracts.
- Server errors must be translated into typed client exceptions.
- Application messages must use stable `MessageCode` values.
- Constants must replace magic numbers and unexplained literals.
- Functions must be named according to what they actually do.
- Existing functions must be reused before creating new ones.
- Important functions and code sections must be commented.
- Documentation must be updated whenever production behavior, architecture, contracts, or rules change.

---

# 1. Core WPF Architecture

Use the following responsibility boundaries:

```text
XAML View
    ↓ Binding / Command
ViewModel
    ↓
Service Interface
    ↓
Service Implementation
    ↓
API Client / HttpClient
    ↓
Server API
```

The View is responsible for:

- UI layout.
- Bindings.
- Styles.
- Templates.
- Visual states.
- Purely visual interaction.

The ViewModel is responsible for:

- Presentation state.
- Commands.
- Loading state.
- Validation state.
- Calling services.
- Mapping service results into UI-friendly state.
- Translating service failures into user-facing state.
- Navigation requests through abstractions.
- Cancellation of user-triggered asynchronous work when appropriate.

The Service layer is responsible for:

- Calling server APIs.
- Serializing request DTOs.
- Deserializing response DTOs.
- Handling HTTP transport concerns.
- Translating server error responses into typed client exceptions.
- Applying client-side retry or timeout policy only when explicitly required.
- Returning clean results to ViewModels.

---

# 2. Recommended Project Structure

```text
Application.Wpf/
│
├── Views/
│   ├── MainWindow.xaml
│   ├── Accounts/
│   │   ├── AccountPageView.xaml
│   │   └── Components/
│   │       ├── AccountFilterView.xaml
│   │       ├── AccountListView.xaml
│   │       └── AccountDetailView.xaml
│   └── Shared/
│
├── ViewModels/
│   ├── Base/
│   │   └── ViewModelBase.cs
│   ├── Accounts/
│   │   ├── AccountPageViewModel.cs
│   │   └── Components/
│   │       ├── AccountFilterViewModel.cs
│   │       ├── AccountListViewModel.cs
│   │       └── AccountDetailViewModel.cs
│   └── MainWindowViewModel.cs
│
├── Models/
│   ├── AccountDisplayModel.cs
│   └── CustomerDisplayModel.cs
│
├── DTOs/
│   ├── Requests/
│   │   ├── CreateAccountRequest.cs
│   │   └── UpdateAccountRequest.cs
│   ├── Responses/
│   │   ├── AccountResponse.cs
│   │   ├── ApiErrorResponse.cs
│   │   └── ApiMessageResponse.cs
│   └── Common/
│
├── Services/
│   ├── Interfaces/
│   │   ├── IAccountService.cs
│   │   ├── IAuthenticationService.cs
│   │   └── INavigationService.cs
│   ├── AccountService.cs
│   ├── AuthenticationService.cs
│   └── NavigationService.cs
│
├── Api/
│   ├── ApiClient.cs
│   ├── ApiResponseReader.cs
│   └── HttpClientRegistration.cs
│
├── Exceptions/
│   ├── AppException.cs
│   ├── ApiException.cs
│   ├── ValidationException.cs
│   ├── AuthenticationException.cs
│   ├── AuthorizationException.cs
│   ├── NotFoundException.cs
│   ├── ConflictException.cs
│   ├── BusinessRuleException.cs
│   ├── NetworkException.cs
│   └── ServiceUnavailableException.cs
│
├── Messages/
│   ├── MessageCode.cs
│   ├── MessageCatalog.cs
│   └── MessageExtensions.cs
│
├── Constants/
│   ├── ApiConstants.cs
│   ├── UiConstants.cs
│   ├── ValidationConstants.cs
│   └── NavigationConstants.cs
│
├── Commands/
│   ├── RelayCommand.cs
│   └── AsyncRelayCommand.cs
│
├── Converters/
│
├── Validation/
│
├── Navigation/
│
├── Logging/
│
├── Resources/
│   ├── Styles/
│   ├── Templates/
│   └── Themes/
│
├── Documentation/
│   ├── Architecture.md
│   ├── MessageCodes.md
│   ├── ApiContracts.md
│   └── UIFlows.md
│
├── App.xaml
├── App.xaml.cs
└── appsettings.json
```

Do not introduce additional layers unless they solve a real problem.

Do not create duplicate abstractions for responsibilities that already exist.

---

# 3. View Rules

Views must be declarative and thin.

Views may contain:

- XAML layout.
- Bindings.
- Resource references.
- Data templates.
- Control templates.
- Styles.
- Visual states.
- Purely visual event handling when binding or commands are not practical.

Views must not contain:

- Business logic.
- API calls.
- Database logic.
- Business validation.
- Application workflow logic.
- Direct construction of services.
- Complex state management.

Bad:

```csharp
private async void SaveButton_Click(
    object sender,
    RoutedEventArgs e)
{
    var client = new HttpClient();

    var response = await client.PostAsJsonAsync(
        "/api/accounts",
        new
        {
            Name = AccountNameTextBox.Text
        });

    if (!response.IsSuccessStatusCode)
    {
        MessageBox.Show("Error");
    }
}
```

Preferred:

```xml
<Button
    Content="Save"
    Command="{Binding SaveAccountCommand}" />
```

The command belongs in the ViewModel.

---

# 4. Code-Behind Rules

Code-behind is allowed only for View-specific concerns that are difficult or inappropriate to represent through bindings, behaviors, commands, or ViewModel state.

Acceptable examples:

- Window drag behavior.
- Focus management.
- View-only animation.
- Control-specific UI behavior.
- Interop with APIs that require a `Window` or control instance.

Code-behind must not contain business or API logic.

When code-behind is used, add a comment explaining why the logic belongs in the View.

---

# 5. ViewModel Responsibilities

ViewModels manage UI state and presentation behavior.

ViewModels may:

- Expose bindable properties.
- Expose commands.
- Call services.
- Track `IsBusy`.
- Track validation messages.
- Track error or information state.
- Convert DTOs into display state.
- Trigger navigation through an abstraction.
- Coordinate multiple UI operations.

ViewModels must not:

- Use `HttpClient` directly.
- Access server endpoints directly.
- Depend directly on `Window`, `Page`, `UserControl`, or other visual controls.
- Use `MessageBox.Show()` directly unless the architecture explicitly permits it through a UI abstraction.
- Contain persistence logic.
- Contain low-level transport logic.

# 6. Component-Based MVVM With Sub-ViewModels

UI structure must be component-based.

Large screens must be composed from focused sub-ViewModels rather than implemented as one large ViewModel.

Preferred structure:

```text
MainWindowView
    ↓
MainWindowViewModel
    ├── SidebarViewModel
    ├── HeaderViewModel
    └── CurrentPageViewModel

AccountPageView
    ↓
AccountPageViewModel
    ├── AccountFilterViewModel
    ├── AccountListViewModel
    ├── AccountDetailViewModel
    └── AccountActionViewModel
```

Each sub-ViewModel owns one focused part of the screen.

A parent ViewModel coordinates the screen.

A sub-ViewModel should not become a general-purpose container for unrelated UI behavior.

---

# 7. Sub-ViewModel Responsibilities

A sub-ViewModel should own a coherent UI component.

Examples:

```text
SearchViewModel
FilterViewModel
PaginationViewModel
AccountListViewModel
AccountDetailViewModel
AccountFormViewModel
TransactionSummaryViewModel
ToolbarViewModel
StatusBarViewModel
```

A sub-ViewModel may own:

- Bindable state for its component.
- Commands related to that component.
- Component-specific validation.
- Component-specific loading state.
- Calls to relevant Services.
- Child ViewModels when further decomposition is justified.

A sub-ViewModel must not:

- Control unrelated sections of the screen.
- Directly manipulate another component's controls.
- Reach into another ViewModel's private state.
- Become a dumping ground for shared logic.

---

# 8. Parent and Child ViewModel Composition

Parent ViewModels should expose sub-ViewModels as properties.

Example:

```csharp
public sealed class AccountPageViewModel : ViewModelBase
{
    public AccountFilterViewModel Filter { get; }

    public AccountListViewModel AccountList { get; }

    public AccountDetailViewModel AccountDetail { get; }

    public AccountActionViewModel Actions { get; }

    // Composes the account screen from focused sub-ViewModels.
    public AccountPageViewModel(
        AccountFilterViewModel filter,
        AccountListViewModel accountList,
        AccountDetailViewModel accountDetail,
        AccountActionViewModel actions)
    {
        Filter = filter;
        AccountList = accountList;
        AccountDetail = accountDetail;
        Actions = actions;
    }
}
```

XAML should bind components to their corresponding sub-ViewModels.

Example:

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="280" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <local:AccountFilterView
        DataContext="{Binding Filter}" />

    <local:AccountListView
        Grid.Column="1"
        DataContext="{Binding AccountList}" />
</Grid>
```

Do not flatten every property from every component into one parent ViewModel only to make binding shorter.

---

# 9. Component View Rules

Reusable UI components should normally have:

```text
ComponentView.xaml
ComponentViewModel.cs
```

Example:

```text
Views/
└── Accounts/
    ├── AccountPageView.xaml
    ├── Components/
    │   ├── AccountFilterView.xaml
    │   ├── AccountListView.xaml
    │   └── AccountDetailView.xaml

ViewModels/
└── Accounts/
    ├── AccountPageViewModel.cs
    ├── Components/
    │   ├── AccountFilterViewModel.cs
    │   ├── AccountListViewModel.cs
    │   └── AccountDetailViewModel.cs
```

Use component-specific folders when a feature has multiple reusable UI pieces.

Do not create a separate ViewModel for a purely visual component that has no independent state or behavior.

---

# 10. Sub-ViewModel Communication

Sub-ViewModels must not tightly couple themselves to each other.

Avoid:

```csharp
AccountListViewModel.AccountDetailViewModel.AccountFormViewModel...
```

Avoid directly mutating another sub-ViewModel's internal fields.

Preferred communication options, in order:

1. Parent ViewModel coordination.
2. Shared focused service.
3. Event/callback abstraction.
4. Messenger/event aggregator only when decoupled cross-component communication is genuinely needed.

Example using parent coordination:

```csharp
public sealed class AccountPageViewModel : ViewModelBase
{
    public AccountListViewModel AccountList { get; }

    public AccountDetailViewModel AccountDetail { get; }

    // Coordinates selection changes between sibling components.
    private void HandleSelectedAccountChanged(
        AccountDisplayModel? account)
    {
        AccountDetail.SetAccount(account);
    }
}
```

Prefer explicit coordination over hidden global messaging.

---

# 11. Shared State Between Sub-ViewModels

When multiple sub-ViewModels require the same mutable screen state, use one explicit shared state object rather than duplicating the data.

Example:

```csharp
public sealed class AccountPageState : ViewModelBase
{
    private AccountDisplayModel? _selectedAccount;

    public AccountDisplayModel? SelectedAccount
    {
        get => _selectedAccount;
        set => SetProperty(ref _selectedAccount, value);
    }
}
```

Then inject the same state object into relevant sub-ViewModels.

Use shared state only when the state is truly shared.

Do not turn shared state into a global application state container.

---

# 12. Sub-ViewModel Dependency Rules

A sub-ViewModel may depend on:

- Focused Services.
- Shared feature state.
- Navigation abstractions.
- Dialog abstractions.
- Logging.
- Other infrastructure abstractions that it genuinely needs.

A sub-ViewModel should not depend on:

- Parent View instances.
- Child View instances.
- `Window`.
- `UserControl`.
- Another sub-ViewModel merely to access its data.

Prefer passing required state or using an explicit shared state abstraction.

---

# 13. Sub-ViewModel Lifecycle

Sub-ViewModels that load data or hold resources should have an explicit lifecycle.

Recommended patterns:

```text
InitializeAsync
RefreshAsync
ActivateAsync
DeactivateAsync
Dispose
```

Use only the lifecycle methods actually needed.

Example:

```csharp
public interface IAsyncInitializable
{
    Task InitializeAsync(
        CancellationToken cancellationToken);
}
```

Parent ViewModels should coordinate child initialization when the screen opens.

Example:

```csharp
// Initializes each screen component required before the page is presented.
public async Task InitializeAsync(
    CancellationToken cancellationToken)
{
    await Filter.InitializeAsync(cancellationToken);
    await AccountList.InitializeAsync(cancellationToken);
}
```

Do not perform long-running API calls inside constructors.

Constructors should establish dependencies and initial state only.

---

# 14. Sub-ViewModel Disposal

If a sub-ViewModel subscribes to events, timers, message buses, or unmanaged resources, it must unsubscribe or dispose them.

Use:

```text
IDisposable
IAsyncDisposable
```

when appropriate.

Parent ViewModels are responsible for disposing owned child ViewModels unless the DI container owns their lifetime.

Do not leave event subscriptions that keep closed screens alive in memory.

---

# 15. Avoid Monolithic ViewModels

A ViewModel should be split when it starts owning multiple independent UI regions or unrelated responsibilities.

Warning signs:

```text
Hundreds of properties in one ViewModel
Many unrelated commands
Multiple independent loading states
Multiple independent validation groups
Large switch statements for unrelated UI areas
One ViewModel managing several tabs or panels directly
Frequent changes to one screen component breaking another
```

Preferred:

```text
AccountPageViewModel
    ├── AccountHeaderViewModel
    ├── AccountListViewModel
    ├── AccountDetailViewModel
    └── AccountTransactionsViewModel
```

instead of:

```text
AccountPageViewModel
    ├── 40+ properties
    ├── 20+ commands
    ├── multiple unrelated service calls
    └── all screen behavior
```

---

# 16. One Model Used by Multiple ViewModels

A single DTO or model may be represented by multiple ViewModels when different UI components require different presentation behavior.

Example:

```text
AccountResponse
    ├── AccountListItemViewModel
    ├── AccountDetailViewModel
    └── AccountEditViewModel
```

Do not force one ViewModel to support every possible presentation of the same model.

---

# 17. One ViewModel Combining Multiple Models

A focused component ViewModel may combine data from multiple API models when the component genuinely presents them together.

Example:

```text
CustomerAccountSummaryViewModel
    ├── CustomerResponse
    ├── AccountResponse
    └── BalanceSummaryResponse
```

The composition must serve one clear presentation responsibility.

Do not use this as justification for creating large all-purpose ViewModels.

---

# 18. ViewModel Composition Over Inheritance

Prefer composing ViewModels from smaller ViewModels over creating deep ViewModel inheritance hierarchies.

Preferred:

```text
AccountPageViewModel
    ├── AccountFilterViewModel
    ├── AccountListViewModel
    └── AccountDetailViewModel
```

Avoid:

```text
BaseViewModel
    ↓
PageViewModel
    ↓
AccountPageBaseViewModel
    ↓
AccountPageViewModel
```

unless inheritance represents a genuine stable abstraction.

Use inheritance mainly for shared infrastructure such as:

```text
INotifyPropertyChanged
Common lifecycle contract
Common disposal support
```

not for sharing feature behavior.

---

---

# 19. ViewModel Base Class

Use a shared base class when the project already has one.

Do not create another base class if one already provides the required property-notification behavior.

Example:

```csharp
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // Raises PropertyChanged only when the property value actually changes.
    protected bool SetProperty<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));

        return true;
    }
}
```

If the project already uses a toolkit or another base implementation, reuse it instead of introducing a competing pattern.

---

# 20. Property Change Notification

Bindable ViewModel properties must notify the View when their values change.

Example:

```csharp
private string _accountName = string.Empty;

public string AccountName
{
    get => _accountName;

    set
    {
        // Update the property only when its value has actually changed.
        if (SetProperty(ref _accountName, value))
        {
            ValidateAccountName();
        }
    }
}
```

Do not manually raise `PropertyChanged` for unrelated properties unless required.

If a calculated property depends on another property, notify it explicitly.

---

# 21. Commands

User actions should normally be exposed through commands.

Examples:

```text
LoadAccountsCommand
CreateAccountCommand
SaveAccountCommand
DeleteAccountCommand
RefreshAccountsCommand
CancelCommand
NavigateBackCommand
```

Command names must describe the action.

Avoid:

```text
ButtonCommand
DoCommand
ActionCommand
HandleCommand
ProcessCommand
```

Commands should delegate complex business behavior to services or focused ViewModel methods.

---

# 22. Async Commands

Network operations must not block the UI thread.

Prefer asynchronous commands for server calls.

Example:

```csharp
// Saves the current account through the application service without blocking the UI thread.
private async Task SaveAccountAsync(
    CancellationToken cancellationToken)
{
    if (IsBusy)
    {
        return;
    }

    try
    {
        // Prevent duplicate user operations while the current save is running.
        IsBusy = true;

        var request = new CreateAccountRequest(
            AccountName,
            SelectedAccountType,
            OpeningBalance);

        var account = await _accountService.CreateAccountAsync(
            request,
            cancellationToken);

        StatusMessageCode =
            MessageCode.AccountCreatedSuccessfully;

        CurrentAccount =
            AccountDisplayModel.FromResponse(account);
    }
    catch (AppException exception)
    {
        HandleApplicationException(exception);
    }
    finally
    {
        IsBusy = false;
    }
}
```

Never use:

```csharp
.Result
.Wait()
.GetAwaiter().GetResult()
```

on the UI thread.

---

# 23. Service Layer

ViewModels must communicate with the server only through service interfaces.

Example:

```csharp
public interface IAccountService
{
    Task<AccountResponse> GetAccountByIdAsync(
        long accountId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AccountResponse>> GetAccountsAsync(
        CancellationToken cancellationToken);

    Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken);
}
```

The ViewModel depends on `IAccountService`.

It must not depend on `AccountService` directly unless the project intentionally does not abstract services.

---

# 24. API Service Implementation

Services encapsulate endpoint and transport details.

Example:

```csharp
public sealed class AccountService : IAccountService
{
    private readonly ApiClient _apiClient;

    public AccountService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // Retrieves an account from the server by its unique identifier.
    public Task<AccountResponse> GetAccountByIdAsync(
        long accountId,
        CancellationToken cancellationToken)
    {
        var endpoint =
            $"{ApiConstants.AccountsEndpoint}/{accountId}";

        return _apiClient.GetAsync<AccountResponse>(
            endpoint,
            cancellationToken);
    }

    // Sends a request to create an account and returns the server response.
    public Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<
            CreateAccountRequest,
            AccountResponse>(
            ApiConstants.AccountsEndpoint,
            request,
            cancellationToken);
    }
}
```

Do not duplicate common HTTP parsing logic across multiple services.

---

# 25. API Client Responsibility

Shared HTTP behavior should live in a reusable API client or response reader.

The API client may handle:

- Base address.
- Authentication headers.
- JSON serialization.
- JSON deserialization.
- Standard error parsing.
- Trace identifiers.
- Cancellation.
- Transport exceptions.
- Timeouts.
- Common headers.

The API client must not contain business logic specific to accounts, customers, transactions, or other features.

---

# 26. DTO Rules

DTOs represent client/server contracts.

Use separate request and response DTOs.

Examples:

```text
CreateAccountRequest
UpdateAccountRequest
AccountResponse
ApiErrorResponse
ApiMessageResponse
```

Do not bind EF Core entities or server domain entities directly into WPF.

Do not assume server-side models and client display models must be identical.

---

# 27. Display Models

Use display or presentation models when the UI requires state that does not belong in API DTOs.

Examples:

```text
IsSelected
DisplayName
FormattedBalance
StatusText
IsExpanded
IsEditable
```

Do not pollute API DTOs with purely local UI state.

Example:

```csharp
public sealed class AccountDisplayModel
{
    public long Id { get; init; }

    public string AccountNumber { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public decimal Balance { get; init; }

    // Creates a UI display model from a server response DTO.
    public static AccountDisplayModel FromResponse(
        AccountResponse response)
    {
        return new AccountDisplayModel
        {
            Id = response.Id,
            AccountNumber = response.AccountNumber,
            Name = response.Name,
            Balance = response.Balance
        };
    }
}
```

---

# 28. Function and Method Naming Rules

Every function or method must be named according to what it actually does.

Preferred:

```text
LoadAccountsAsync
CreateAccountAsync
RefreshAccountsAsync
ValidateAccountName
CalculateDisplayBalance
HandleApplicationException
MapAccountResponse
NavigateToAccountDetails
```

Avoid vague names:

```text
Handle
Process
Execute
Run
DoWork
DoSomething
Manage
Helper
Check
```

Generic names are acceptable only when the surrounding abstraction makes the behavior completely clear.

Async methods must use the `Async` suffix.

Boolean functions should read like questions where practical:

```text
CanSaveAccount
HasValidationErrors
IsAuthenticationRequired
ShouldRefreshAccounts
```

---

# 29. Reuse Existing Functions Before Creating New Ones

Before creating a new function:

1. Search existing ViewModels.
2. Search existing Services.
3. Search existing API helpers.
4. Search utility and extension classes.
5. Search existing commands.
6. Search validators.
7. Search navigation abstractions.
8. Search message-handling code.

Do not create duplicate functions that perform the same job.

Create a new function only when:

- The behavior is genuinely different.
- Reuse would create misleading naming.
- Reuse would violate separation of responsibilities.
- A new method significantly improves clarity or testability.

---

# 30. Constants and Magic Values

Never use unexplained magic numbers, magic strings, or business-critical literal values directly in ViewModels or Services.

Bad:

```csharp
if (retryCount >= 3)
{
    return;
}

await Task.Delay(2000);

if (name.Length > 100)
{
}
```

Preferred:

```csharp
public static class ApiConstants
{
    public const int MaximumRetryCount = 3;
}

public static class UiConstants
{
    public static readonly TimeSpan SearchDebounceDelay =
        TimeSpan.FromMilliseconds(300);
}

public static class ValidationConstants
{
    public const int MaximumAccountNameLength = 100;
}
```

Use configuration for values that vary by environment.

Examples:

```text
Server base URL
API timeout
Feature flags
Environment-specific endpoints
Logging levels
```

---

# 31. Endpoint Constants

Do not scatter endpoint strings throughout the codebase.

Bad:

```csharp
await client.GetAsync("/api/accounts");
await client.PostAsync("/api/accounts", content);
```

Preferred:

```csharp
public static class ApiConstants
{
    public const string AccountsEndpoint =
        "/api/accounts";

    public const string CustomersEndpoint =
        "/api/customers";

    public const string AuthenticationEndpoint =
        "/api/auth";
}
```

Use URI composition helpers where appropriate.

---

# 32. Comments Are Required

Comments are mandatory before significant functions and non-obvious code sections.

Every public or significant private function must have a comment describing its responsibility.

Example:

```csharp
// Loads all accounts from the server and updates the bound account collection.
private async Task LoadAccountsAsync(
    CancellationToken cancellationToken)
{
    ...
}
```

Important code sections must also have a short comment:

```csharp
// Preserve the previous selection when refreshing the collection.
var selectedAccountId = SelectedAccount?.Id;

// Replace the collection only after the API request succeeds.
Accounts = new ObservableCollection<AccountDisplayModel>(
    accounts);
```

Comments must explain intent, behavior, business rules, or implementation reasons.

Avoid comments that only repeat syntax.

---

# 33. XML Documentation

Use XML documentation for important reusable APIs.

Example:

```csharp
/// <summary>
/// Retrieves account information from the server.
/// </summary>
/// <param name="accountId">
/// The unique account identifier.
/// </param>
/// <param name="cancellationToken">
/// Cancels the pending network operation.
/// </param>
/// <returns>
/// The account response returned by the server.
/// </returns>
Task<AccountResponse> GetAccountByIdAsync(
    long accountId,
    CancellationToken cancellationToken);
```

---

# 34. Message Code Architecture

Every meaningful application message must have a stable `MessageCode`.

Messages may originate from:

- The server.
- Client-side validation.
- Client infrastructure.
- Local UI behavior.

Message text may change.

Message codes must remain stable.

The application should make behavior decisions using message codes rather than message text.

Bad:

```csharp
if (exception.Message == "Account not found.")
{
}
```

Preferred:

```csharp
if (exception.Code == MessageCode.AccountNotFound)
{
}
```

---

# 35. Message Code Ranges

Use the same stable message-code ranges as the server where shared meaning exists.

Recommended structure:

```text
1000 - 1999   Success
2000 - 2999   Information
3000 - 3999   Validation
4000 - 4099   Authentication
4100 - 4199   Authorization
4200 - 4299   Not Found
4300 - 4399   Conflict
4400 - 4499   Business Rule
5000 - 5999   Server / Infrastructure
6000 - 6999   Client / WPF Infrastructure
```

Client-only codes should use a dedicated range to avoid collision with server codes.

Example:

```text
6000 ClientError
6001 NetworkUnavailable
6002 RequestTimeout
6003 InvalidServerResponse
6004 NavigationFailed
6005 LocalStateError
```

Do not reuse existing server values for different client meanings.

---

# 36. MessageCode Enum

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

    // Information: 2000 - 2999
    Information = 2000,
    NoChangesDetected = 2001,
    ProcessingStarted = 2002,
    ProcessingCompleted = 2003,

    // Validation: 3000 - 3999
    ValidationFailed = 3000,
    RequiredFieldMissing = 3001,
    InvalidRequest = 3002,
    InvalidAmount = 3003,
    InvalidDate = 3004,
    AccountNameRequired = 3100,
    AccountTypeInvalid = 3101,
    OpeningBalanceInvalid = 3102,

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

    // Server / Infrastructure: 5000 - 5999
    InternalServerError = 5000,
    DatabaseError = 5001,
    ExternalServiceUnavailable = 5002,
    TimeoutOccurred = 5003,
    ServiceUnavailable = 5004,

    // Client / WPF: 6000 - 6999
    ClientError = 6000,
    NetworkUnavailable = 6001,
    RequestTimeout = 6002,
    InvalidServerResponse = 6003,
    NavigationFailed = 6004,
    LocalStateError = 6005
}
```

---

# 37. Message Catalog

Use a central message catalog for client-visible messages.

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

            [MessageCode.NetworkUnavailable] =
                "The server cannot currently be reached.",

            [MessageCode.RequestTimeout] =
                "The request timed out.",

            [MessageCode.InvalidServerResponse] =
                "The server returned an invalid response.",

            [MessageCode.InternalServerError] =
                "An unexpected server error occurred."
        };

    // Resolves a human-readable message from a stable application message code.
    public static string GetMessage(
        MessageCode code)
    {
        return Messages.TryGetValue(
            code,
            out var message)
            ? message
            : "Unknown application message.";
    }
}
```

Do not scatter UI strings for errors and business outcomes throughout ViewModels.

---

# 38. Client Exception Structure

Use typed exceptions.

Recommended hierarchy:

```text
Exception
    ↓
AppException
    ├── ApiException
    │   ├── ValidationException
    │   ├── AuthenticationException
    │   ├── AuthorizationException
    │   ├── NotFoundException
    │   ├── ConflictException
    │   └── BusinessRuleException
    │
    ├── NetworkException
    └── ServiceUnavailableException
```

Every expected client application exception must contain a `MessageCode`.

Example:

```csharp
public abstract class AppException : Exception
{
    public MessageCode Code { get; }

    // Creates a client application exception from a stable message code.
    protected AppException(
        MessageCode code)
        : base(MessageCatalog.GetMessage(code))
    {
        Code = code;
    }

    // Creates a client application exception with a contextual message.
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

# 39. Server Error Response

The WPF client should expect a predictable server error contract.

Example:

```csharp
public sealed record ApiErrorResponse(
    int Code,
    string Name,
    string Message,
    string? TraceId);
```

Example JSON:

```json
{
  "code": 4201,
  "name": "AccountNotFound",
  "message": "Account was not found.",
  "traceId": "00-b942..."
}
```

The client must use `code` as the stable identifier.

The `message` is for display.

The `traceId` is useful for support and diagnostics.

---

# 40. Translating HTTP Failures Into Client Exceptions

The API layer must translate HTTP results into typed application exceptions.

ViewModels should not switch directly on HTTP status codes.

Bad:

```csharp
if (response.StatusCode == HttpStatusCode.NotFound)
{
    ErrorText = "Account not found";
}
```

Preferred flow:

```text
HTTP 404
    ↓
ApiClient parses ApiErrorResponse
    ↓
NotFoundException(MessageCode.AccountNotFound)
    ↓
ViewModel handles AppException
    ↓
UI displays MessageCatalog / server message
```

---

# 41. Network and Transport Exceptions

Transport failures are different from server business errors.

Examples:

```text
No network connection
DNS failure
Connection refused
TLS failure
Timeout
Request cancelled
Invalid JSON response
```

Translate these into client-side exceptions or states.

Example mapping:

```text
HttpRequestException
    → NetworkException
    → MessageCode.NetworkUnavailable

Timeout
    → ServiceUnavailableException
    → MessageCode.RequestTimeout

Invalid response JSON
    → ApiException
    → MessageCode.InvalidServerResponse
```

Do not expose raw framework exception messages directly to users.

---

# 42. API Client Example

```csharp
public sealed class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // Sends a GET request and returns the deserialized server response.
    public async Task<TResponse> GetAsync<TResponse>(
        string endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            // Send the request while preserving cancellation from the ViewModel.
            using var response =
                await _httpClient.GetAsync(
                    endpoint,
                    cancellationToken);

            return await ReadResponseAsync<TResponse>(
                response,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new ServiceUnavailableException(
                MessageCode.RequestTimeout);
        }
        catch (HttpRequestException exception)
        {
            throw new NetworkException(
                MessageCode.NetworkUnavailable,
                exception);
        }
    }

    // Reads either a successful DTO response or a standardized server error response.
    private static async Task<TResponse> ReadResponseAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        ...
    }
}
```

Do not duplicate this response-parsing logic in each feature service.

---

# 43. ViewModel Exception Handling

ViewModels should handle typed application exceptions at the presentation boundary.

Example:

```csharp
// Converts an application exception into bindable UI error state.
private void HandleApplicationException(
    AppException exception)
{
    ErrorMessageCode = exception.Code;
    ErrorMessage = exception.Message;
}
```

Do not catch `Exception` everywhere.

Catch a general exception only at a final application boundary where it can be logged and converted into a safe generic UI message.

---

# 44. Global WPF Exception Handling

The application should have a final safety net for otherwise unhandled exceptions.

Possible application-level hooks include:

```text
Application.DispatcherUnhandledException
AppDomain.CurrentDomain.UnhandledException
TaskScheduler.UnobservedTaskException
```

These handlers must:

- Log the real exception.
- Avoid exposing stack traces to users.
- Display a safe generic message.
- Preserve diagnostic identifiers where useful.

They must not be used as a substitute for normal exception handling in Services and ViewModels.

---

# 45. Validation

Separate local UI validation from server business validation.

Local validation examples:

```text
Required field
Invalid numeric input
Invalid date format
Maximum text length
Missing selection
```

Server/business validation examples:

```text
Account is frozen
Insufficient balance
Duplicate account number
Transaction exceeds server-defined limit
Account has not matured
Operation not permitted
```

Local validation should prevent obviously invalid requests.

Server validation remains authoritative.

---

# 46. Validation Message Codes

Local validation messages must also use `MessageCode`.

Example:

```csharp
// Validates the required account name before the API request is sent.
private void ValidateAccountName()
{
    AccountNameErrorCode =
        string.IsNullOrWhiteSpace(AccountName)
            ? MessageCode.AccountNameRequired
            : null;
}
```

Do not rely on arbitrary string comparisons to determine validation state.

---

# 47. Loading State

Async API operations must expose loading state when relevant.

Example:

```csharp
private bool _isBusy;

public bool IsBusy
{
    get => _isBusy;
    set => SetProperty(ref _isBusy, value);
}
```

Use loading state to prevent duplicate commands where necessary.

Command `CanExecute` should reflect relevant state.

Example:

```text
Can save only when:
- not busy
- required fields are valid
- user has permission
```

---

# 48. Cancellation

Long-running or user-triggered API calls should support cancellation where appropriate.

Examples:

```text
Search
Refresh
Large list loading
Navigation-triggered loading
Long-running reports
```

Cancellation should propagate:

```text
ViewModel
    ↓
Service
    ↓
ApiClient
    ↓
HttpClient
```

Do not convert intentional cancellation into an error message.

---

# 49. Observable Collections

Use `ObservableCollection<T>` when the UI needs to react to collection changes.

Do not replace it with `List<T>` when item addition/removal must update the UI.

For large result sets, consider pagination or virtualized controls instead of loading everything into memory.

---

# 50. UI Thread Rules

WPF-bound state must be updated on the UI thread.

Normal awaited ViewModel code generally resumes on the captured UI context.

Do not manually create background threads for routine API calls.

If work is intentionally performed off the UI thread, marshal bound state changes back to the Dispatcher.

Do not use `Task.Run` merely to wrap naturally asynchronous HTTP calls.

---

# 51. Navigation

Navigation should be abstracted from ViewModels.

Preferred:

```csharp
public interface INavigationService
{
    void NavigateTo<TViewModel>();
    bool CanGoBack { get; }
    void GoBack();
}
```

ViewModels must not directly instantiate Windows or Views.

Bad:

```csharp
new AccountWindow().Show();
```

inside a ViewModel.

---

# 52. Dependency Injection

Use constructor injection.

Example:

```csharp
public sealed class AccountListViewModel
{
    private readonly IAccountService _accountService;

    public AccountListViewModel(
        IAccountService accountService)
    {
        _accountService = accountService;
    }
}
```

Services and ViewModels should be registered in the application's DI composition root.

Avoid service locator patterns.

---

# 53. HttpClient

Do not instantiate a new `HttpClient` for every request.

Bad:

```csharp
using var client = new HttpClient();
```

inside service methods.

Prefer a centrally configured `HttpClient`.

The base address, timeout, headers, authentication behavior, and serialization defaults should be configured in one place.

---

# 54. Authentication

Authentication concerns belong in dedicated services or API infrastructure.

Examples:

```text
Login
Logout
Access token storage
Refresh token flow
Authentication header attachment
Expired session handling
```

Do not duplicate authentication header logic across feature services.

Do not store secrets in plain text.

Do not log authentication tokens.

---

# 55. Authorization

The server is the authority for authorization.

The WPF client may hide or disable UI actions for usability, but this is not a security boundary.

Never assume a disabled button makes an operation secure.

Server-side authorization must still reject unauthorized operations.

---

# 56. Logging

Use structured logging.

Preferred:

```csharp
_logger.LogInformation(
    "Loaded {AccountCount} accounts from the server",
    accounts.Count);
```

For errors:

```csharp
_logger.LogError(
    exception,
    "Account load failed. MessageCode: {MessageCode}",
    exception.Code);
```

Never log:

- Passwords.
- Access tokens.
- Refresh tokens.
- PINs.
- Full card numbers.
- Secret keys.
- Connection strings.
- Sensitive personal information.

---

# 57. Date and Time

Prefer UTC for server timestamps and transport.

Convert to local display time only at the presentation boundary when required.

Do not mix server UTC timestamps and local timestamps without explicit conversion.

---

# 58. Money

Use `decimal` for monetary values.

Never use:

```csharp
float
double
```

for financial calculations.

Formatting currency for display belongs in presentation logic, converters, or display models.

Do not round financial values merely for UI display if that rounded value will later be used for business calculations.

---

# 59. XAML Binding Rules

Prefer bindings over manual control mutation.

Bad:

```csharp
AccountNameTextBox.Text = account.Name;
BalanceTextBlock.Text = account.Balance.ToString();
```

Preferred:

```xml
<TextBox Text="{Binding AccountName}" />

<TextBlock Text="{Binding Balance}" />
```

Bindings should have explicit modes when behavior is not obvious.

Examples:

```text
OneWay
TwoWay
OneTime
```

Use `UpdateSourceTrigger` deliberately.

---

# 60. Avoid ViewModel-to-View Coupling

ViewModels must not directly reference:

```text
Window
Page
UserControl
TextBox
Button
DataGrid
DispatcherObject
```

unless a project-specific abstraction explicitly requires it.

UI behavior should be represented through state or services.

---

# 61. Resource Usage

Reusable styles, templates, brushes, and UI constants should live in resources instead of being duplicated.

Do not duplicate the same style across many views.

Use:

```text
StaticResource
DynamicResource
ResourceDictionary
```

appropriately.

---

# 62. Error Display

The UI should expose error state in a consistent way.

Possible properties:

```text
ErrorMessageCode
ErrorMessage
HasError
StatusMessageCode
StatusMessage
```

Do not scatter `MessageBox.Show()` calls throughout ViewModels.

If modal messages are required, use an abstraction such as:

```csharp
IUserDialogService
```

---

# 63. Testing Expectations

Prioritize unit tests for:

```text
ViewModel commands
ViewModel state transitions
Service error translation
MessageCode mapping
Validation
Navigation decisions
Loading state
Cancellation behavior
API response parsing
Exception mapping
```

Tests should not require a real WPF window for pure ViewModel logic.

Prefer testing:

```text
Given server returns AccountNotFound
When LoadAccountAsync executes
Then:
- ErrorMessageCode == AccountNotFound
- IsBusy == false
- no unhandled exception escapes
```

---

# 64. Documentation Update Rule

Documentation is part of every production change.

Whenever production code is created, modified, renamed, moved, or removed, review the related documentation and update it in the same change when behavior, architecture, contracts, or usage change.

Examples that require documentation updates:

```text
New ViewModel
New Service
New API endpoint usage
Changed DTO
Changed MessageCode
New client-only MessageCode
Changed validation rule
Changed navigation flow
Changed exception mapping
Changed constant
Changed application configuration
Changed login/session behavior
Changed project structure
```

Relevant files may include:

```text
README.md
AGENTS-WPF.md
Documentation/Architecture.md
Documentation/MessageCodes.md
Documentation/ApiContracts.md
Documentation/UIFlows.md
XML documentation
```

A code change is not complete until required documentation changes are complete.

---

# 65. Code Change Workflow for Agents

Before changing code:

1. Read the relevant View.
2. Read the relevant ViewModel.
3. Read the related Service.
4. Search for existing methods that already perform the required behavior.
5. Search for existing commands.
6. Search for existing constants.
7. Search for existing `MessageCode` values.
8. Search for existing exception types.
9. Search for existing DTOs.
10. Read related documentation.

While changing code:

1. Keep Views thin.
2. Keep API logic out of ViewModels.
3. Reuse existing functions when they already solve the problem.
4. Name new functions according to their behavior.
5. Do not introduce magic numbers.
6. Reuse or define named constants.
7. Add comments before significant functions.
8. Add comments before important non-obvious code sections.
9. Reuse existing `MessageCode` values when meanings match.
10. Add new codes only for genuinely new semantic conditions.
11. Keep server and client-only message ranges separate.
12. Preserve async and cancellation flow.
13. Preserve MVVM boundaries.

After changing code:

1. Check for duplicate methods.
2. Check for duplicate business or presentation logic.
3. Check for magic numbers and strings.
4. Check comments and XML documentation.
5. Check `MessageCode` consistency.
6. Check exception translation.
7. Check UI loading and error state.
8. Check commands and `CanExecute`.
9. Check tests.
10. Update related documentation.

---

# 66. Code Generation Rules for Agents

When generating or modifying code:

1. Follow the existing MVVM architecture.
2. Build complex screens from focused sub-ViewModels/components.
3. Prefer ViewModel composition over monolithic ViewModels.
4. Parent ViewModels should coordinate sibling sub-ViewModels.
5. Do not make sub-ViewModels reach into each other's private state.
6. Do not perform long-running API calls in ViewModel constructors.
7. Keep Views thin.
8. Keep business and API logic out of code-behind.
9. Put presentation state and commands in ViewModels.
10. Put API communication in Services.
11. Do not let ViewModels call `HttpClient` directly.
12. Do not instantiate Views inside ViewModels.
13. Do not expose server domain entities directly to the View.
14. Use DTOs for API contracts.
15. Use display models when UI-only state is needed.
16. Use typed client exceptions.
17. Every expected application exception must contain a `MessageCode`.
18. Every user-facing error or information state must have a `MessageCode`.
19. Reuse server message codes when semantics match.
20. Use a separate client-only message-code range for WPF-specific failures.
21. Search for existing functions before creating new ones.
22. Reuse an existing function when it already provides the required behavior.
23. Do not duplicate business or presentation logic.
24. Name functions according to what they actually do.
25. Use `Async` suffix for asynchronous methods.
26. Avoid vague names such as `Handle`, `Process`, or `DoWork` unless context is fully clear.
27. Never use unexplained magic numbers.
28. Never scatter API endpoint strings.
29. Use named constants for fixed invariants.
30. Use configuration for environment-specific values.
31. Add comments before significant functions.
32. Add comments before non-obvious code sections.
33. Comments must explain intent or reasoning, not just syntax.
34. Use XML documentation for reusable APIs and services.
35. Do not block the UI thread.
36. Do not use `.Result` or `.Wait()` in UI code.
37. Propagate `CancellationToken`.
38. Do not treat intentional cancellation as an error.
39. Use `ObservableCollection<T>` when collection changes must update the UI.
40. Use dependency injection.
41. Use a centrally configured `HttpClient`.
42. Use structured logging.
43. Never log secrets.
44. Use `decimal` for money.
45. Treat the server as authoritative for business rules and authorization.
46. Update related documentation in the same change.
47. Treat documentation updates as part of the definition of done.

# 67. Architectural Rule of Thumb

```text
Visual layout or styling?
    → View

Bindable UI state or command?
    → ViewModel

Server call?
    → Service

Common HTTP behavior?
    → ApiClient

Client/server data contract?
    → DTO

UI-only representation?
    → Display Model

Expected failure?
    → Typed Exception

Application outcome meaning?
    → MessageCode

Human-readable message?
    → MessageCatalog

Fixed invariant?
    → Constant

Environment-specific value?
    → Configuration

Navigation?
    → Navigation Service

Modal UI interaction?
    → Dialog Service
```

---

# 68. Exception Flow

The normal failure path should be:

```text
Server
    ↓
ApiErrorResponse
    ↓
ApiClient
    ↓
Typed AppException
    ↓
Service
    ↓
ViewModel
    ↓
Bindable Error State
    ↓
View
```

Example:

```text
HTTP 422
    ↓
{
    code: 4401,
    name: "InsufficientBalance",
    message: "The account has insufficient balance."
}
    ↓
BusinessRuleException(
    MessageCode.InsufficientBalance)
    ↓
ViewModel
    ↓
ErrorMessageCode = 4401
ErrorMessage = "The account has insufficient balance."
    ↓
View
```

---

# 69. Success Flow

A successful operation should follow:

```text
View
    ↓ Command
ViewModel
    ↓
Service
    ↓
ApiClient
    ↓
Server
    ↓
Response DTO
    ↓
Service
    ↓
ViewModel State
    ↓
View
```

Example:

```text
SaveAccountCommand
    ↓
CreateAccountAsync
    ↓
IAccountService.CreateAccountAsync
    ↓
POST /api/accounts
    ↓
AccountResponse
    ↓
CurrentAccount updated
    ↓
MessageCode.AccountCreatedSuccessfully
```

---

# 70. Definition of Done

A WPF production change is complete only when all applicable items are satisfied:

- Complex screens are composed from focused sub-ViewModels where appropriate.
- Parent and child ViewModel responsibilities are clear.
- Sub-ViewModels do not tightly couple themselves to sibling ViewModels.
- Long-running API work is not started from constructors.
- Owned sub-ViewModels are disposed when required.
- MVVM boundaries are preserved.
- Views remain thin.
- Code-behind contains no business or API logic.
- ViewModels do not call `HttpClient` directly.
- Services encapsulate server communication.
- No duplicate function was created unnecessarily.
- Functions are named according to behavior.
- Async methods use the `Async` suffix.
- No unexplained magic numbers remain.
- Endpoint strings are centralized.
- Constants or configuration are used appropriately.
- Required comments are present.
- Public reusable APIs have useful XML documentation.
- Message codes are correct and stable.
- Client-only message codes do not collide with server codes.
- Server errors are translated into typed client exceptions.
- Raw transport exceptions are not shown directly to users.
- UI loading state is correct.
- Cancellation behavior is correct.
- Commands cannot trigger invalid duplicate operations.
- Tests cover changed ViewModel and Service behavior.
- Related documentation has been updated.
- Documentation examples remain consistent with the implementation.
