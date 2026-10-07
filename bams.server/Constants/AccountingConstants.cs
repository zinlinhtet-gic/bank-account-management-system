namespace bams.server.Constants;


/// <summary>
/// General-ledger account codes used by transaction posting and scheduled operations. Teller-posting accounts are
/// created by <c>Data/Seeders/ChartOfAccountsSeeder</c> and scheduled-operation accounts by <c>ProductSeeder</c>;
/// every posting writes balanced debit and credit lines to them.
/// </summary>
public static class AccountingConstants
{
    // Assets
    public const string CashOnHandGlCode = "1000";
    public const string DueFromOtherBanksGlCode = "1100";
    public const string MaintenanceFeeReceivableGlCode = "1101";
    public const string DormantPenaltyReceivableGlCode = "1102";

    public const int FirstMonthOfYear = 1;
    public const int LastMonthOfYear = 12;

    // Liabilities
    // Liabilities. Every customer balance change, teller or scheduled, hits Customer Deposits.
    public const string CustomerDepositsGlCode = "2000";
    public const string InterbankClearingGlCode = "2100";
    public const string InterestPayableGlCode = "2101";
    public const string NrcTransfersPayableGlCode = "2200";
    
    // Statuses
    public const string ActiveStatus = "Active";

    // GL accounts share the single "Active" spelling so seeders and posting lookups never disagree on case.
    public const string ActiveGlAccountStatus = ActiveStatus;

    // Income
    public const string MaintenanceFeeIncomeGlCode = "4001";
    public const string DormantPenaltyIncomeGlCode = "4002";
    public const string TransferFeeIncomeGlCode = "4003";

    // Expenses
    public const string InterestExpenseGlCode = "6001";

}
