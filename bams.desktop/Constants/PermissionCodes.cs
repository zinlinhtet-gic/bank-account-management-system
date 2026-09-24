namespace bams.desktop.Constants;

/// <summary>
/// Permission codes returned by <c>GET /api/auth/permissions</c>.
/// Values must match <c>bams.server/Constants/SecurityConstants.cs</c> exactly.
/// A mismatch does not throw; it silently hides the related UI, so always use these constants.
/// </summary>
public static class PermissionCodes
{
    public const string UserManagement = "user_management";
    public const string CustomerManagement = "customer_management";
    public const string CustomerKyc = "customer_kyc";
    public const string Accounting = "accounting";
    public const string Configuration = "configuration";
    public const string Operation = "operation";
    public const string AccountManagement = "account_management";
    public const string Transactions = "transactions";
    public const string TransactionHistory = "transaction_history";
    public const string Audit = "audit";
    public const string CustomerList = "customer_list";
}
