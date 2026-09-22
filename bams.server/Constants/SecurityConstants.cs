namespace bams.server.Constants;

/// <summary>
/// Defines role codes and their associated permissions.
/// </summary>
public static class SecurityConstants
{
    // Role codes
    public const string ManagerRole = "manager";
    public const string OfficerRole = "officer";
    public const string AuditorRole = "auditor";

    // Permission codes
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