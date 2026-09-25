namespace bams.desktop.Constants;

/// <summary>
/// Server address and endpoint paths used by the client services.
/// </summary>
public static class ApiConstants
{
    public const string ServerBaseAddress = "http://localhost:5121";

    public const string AuthEndpoint = "api/auth";
    public const string LoginEndpoint = AuthEndpoint + "/login";
    public const string PermissionsEndpoint = AuthEndpoint + "/permissions";
    public const string ChangePasswordEndpoint = AuthEndpoint + "/change-password";
    public const string HeartbeatEndpoint = AuthEndpoint + "/heartbeat";
    public const string LogoutEndpoint = AuthEndpoint + "/logout";

    // User Management. Routes with an id: $"{UsersEndpoint}/{id}" and $"{UsersEndpoint}/{id}/{ResetPasswordSegment}".
    public const string UsersEndpoint = "api/users";
    public const string UserRolesEndpoint = UsersEndpoint + "/roles";
    public const string ResetPasswordSegment = "reset-password";

    // Accounts (the officer's account pickers).
    public const string AccountsEndpoint = "api/accounts";

    // Transactions. Routes with an id: $"{TransactionsEndpoint}/{id}",
    // $"{InterbankTransferEndpoint}/{id}/{CompleteSegment}", $"{NrcTransferEndpoint}/{id}/{CancelSegment}".
    public const string TransactionsEndpoint = "api/transactions";
    public const string DepositEndpoint = TransactionsEndpoint + "/deposit";
    public const string WithdrawalEndpoint = TransactionsEndpoint + "/withdrawal";
    public const string InternalTransferEndpoint = TransactionsEndpoint + "/transfer/internal";
    public const string InterbankTransferEndpoint = TransactionsEndpoint + "/transfer/interbank";
    public const string NrcTransferEndpoint = TransactionsEndpoint + "/transfer/nrc";
    public const string NrcPickupEndpoint = TransactionsEndpoint + "/nrc-pickup";
    public const string OtherBanksEndpoint = TransactionsEndpoint + "/other-banks";
    public const string BranchesEndpoint = TransactionsEndpoint + "/branches";
    public const string PaidOutSegment = "paid-out";
    public const string CompleteSegment = "complete";
    public const string FailSegment = "fail";
    public const string CancelSegment = "cancel";

    // Account statement: $"{TransactionAccountsEndpoint}/{accountId}/{StatementSegment}".
    public const string TransactionAccountsEndpoint = TransactionsEndpoint + "/accounts";
    public const string StatementSegment = "statement";

    // Retrying a posting with the same key returns the first result instead of posting twice.
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public const string BearerScheme = "Bearer";
}
