namespace bams.desktop.Constants;

/// <summary>
/// Navigation labels for every page. The same value links the nav tab (NavBarViewModel),
/// the page ViewModel (NavigationService) and the header title, so always use these constants.
/// </summary>
public static class PageNames
{
    public const string UserManagement = "User Management";
    public const string CustomerManagement = "Customer Management";
    public const string CustomerKyc = "Customer KYC";
    public const string AccountManagement = "Account Management";
    public const string Transactions = "Transactions";
    public const string TransactionHistory = "Transaction History";
    // Parent Navigation groups
    public const string Accounting = "Accounting";
    public const string Audit = "Audit";
    // Accounting child pages
    public const string GeneralLedger = "General Ledger";
    public const string AccountingEntries = "Accounting Entries";
    public const string Reconciliation = "Reconciliation";
    // Audit child pages
    public const string TransactionAudit = "Transaction Audit";
    public const string Operations = "Operations";
    public const string Configurations = "Configurations";
    public const string CustomerList = "Customer List";

    // Configuration sub-pages
    public const string InterestRate = "Interest Rate";
    public const string FeeRate = "Fee Rate";
    public const string BankPolicies = "Bank Policies";
    public const string OtherBanks = "Other Banks";
}
