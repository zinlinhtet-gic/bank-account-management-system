namespace bams.server.Constants;


/// <summary>
/// General-ledger account codes used by transaction posting. The accounts are created by
/// <c>Data/Seeders/ChartOfAccountsSeeder</c>; every posting writes balanced debit and credit lines to them.
/// </summary>
public static class AccountingConstants
{
    // Assets
    public const string CashOnHandGlCode = "1000";
    public const string DueFromOtherBanksGlCode = "1100";

    public const int FirstMonthOfYear = 1;
    public const int LastMonthOfYear = 12;

    // Liabilities
    public const string CustomerDepositsGlCode = "2000";
    public const string InterbankClearingGlCode = "2100";
    public const string NrcTransfersPayableGlCode = "2200";
    
    // Statuses
    // public const string ActiveGlAccountStatus = "active";

    public const string ActiveStatus = "Active";
}
