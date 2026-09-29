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

    // Liabilities. Every customer balance change, teller or scheduled, hits Customer Deposits.
    public const string CustomerDepositsGlCode = "2000";
    public const string InterbankClearingGlCode = "2100";
    public const string InterestPayableGlCode = "2101";
    public const string NrcTransfersPayableGlCode = "2200";

    // Income
    public const string MaintenanceFeeIncomeGlCode = "4001";
    public const string DormantPenaltyIncomeGlCode = "4002";

    // Expenses
    public const string InterestExpenseGlCode = "6001";

    public const string ActiveGlAccountStatus = "active";
}
