namespace bams.desktop.Constants;

/// <summary>
/// Where a notification leads when clicked. Values match bams.server NotificationConstants.Targets.
/// </summary>
public static class NotificationTargets
{
    public const string SessionDetail = "SessionDetail";
    public const string SessionManagement = "SessionManagement";
    public const string CashRequest = "CashRequest";
    public const string CashHandoff = "CashHandoff";
    public const string BankExpense = "BankExpense";
    public const string ReconciliationTask = "ReconciliationTask";
    public const string AccountProposal = "AccountProposal";
    public const string CashProposal = "CashProposal";
    public const string AccountVerification = "AccountVerification";
    public const string CashVerification = "CashVerification";
    public const string ReconciliationChecks = "ReconciliationChecks";

    /// <summary>True when the target refers to a cash (not account/GL) reconciliation record.</summary>
    public static bool IsCashRecord(string targetType) =>
        targetType is CashProposal or CashVerification;
}
