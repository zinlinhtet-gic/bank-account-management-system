namespace bams.server.Constants;

/// <summary>Persisted status and type identifiers for cash operations and business-date processing.</summary>
public static class OperationsConstants
{
    public const int MaximumReconciliationRangeDays = 366;
    public const string BusinessDateOpen = "Open";
    public const string BusinessDateClosed = "Closed";
    public const string CashPositionTeller = "Teller";
    public const string CashPositionVault = "Vault";
    public const string CashSessionOpen = "Open";
    public const string CashSessionClosed = "Closed";
    public const string ReconciliationMatched = "Matched";
    public const string ReconciliationUnmatched = "Unmatched";
    public const string ExceptionOpen = "Open";
    public const string ExceptionUnderInvestigation = "UnderInvestigation";
    public const string ExceptionAdjustmentRequired = "AdjustmentRequired";
    public const string ExceptionResolved = "Resolved";
    public const string ExceptionAccountBalance = "AccountBalance";
    public const string ExceptionCashBalance = "CashBalance";
    public const string ExceptionLedgerBalance = "LedgerBalance";
    public const string EodStarted = "Started";
    public const string EodBlocked = "Blocked";
    public const string EodReadyForApproval = "ReadyForApproval";
    public const string EodApproved = "Approved";
    public const string EodClosed = "Closed";
    public const string EodStageTransactions = "Transactions";
    public const string EodStagePendingApprovals = "Pending Approvals";
    public const string EodStageAccountingEntries = "Accounting Entries";
    public const string EodStageLedgerReconciliation = "Ledger Reconciliation";
    public const string EodStageCash = "Teller/Vault Cash";
    public const string EodStageCriticalExceptions = "Critical Exceptions";
    public const string EodStageAccountReconciliation = "Account Reconciliation";
    public const string CashMovementApproved = "Approved";
    public const string CashMovementPendingApproval = "PendingApproval";
    public const string CashMovementRejected = "Rejected";
    public const string CashMovementAdjustment = "ApprovedAdjustment";
    public const string CashHandoffPendingAcceptance = "PendingAcceptance";
    public const string CashHandoffAccepted = "Accepted";
    public const string CashHandoffDeclined = "Declined";
}
