namespace bams.desktop.Constants;

/// <summary>Stable stage labels returned by the End of Day API.</summary>
public static class EndOfDayStageNames
{
    public const string Transactions = "Transactions";
    public const string PendingApprovals = "Pending Approvals";
    public const string AccountingEntries = "Accounting Entries";
    public const string LedgerReconciliation = "Ledger Reconciliation";
    public const string TellerVaultCash = "Teller/Vault Cash";
    public const string CriticalExceptions = "Critical Exceptions";
    public const string AccountReconciliation = "Account Reconciliation";
}
