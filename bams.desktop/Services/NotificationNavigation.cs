using bams.desktop.Constants;

namespace bams.desktop.Services;

/// <summary>The page a notification opens, and whether the signed-in user may open it.</summary>
public sealed record NotificationPageRoute(string Page, bool IsAllowed);

/// <summary>
/// Maps a notification target to its desktop page using the same permission rules as the sidebar
/// (NavBarViewModel.BuildNavigationItems), so a notification never opens a page the user cannot see.
/// </summary>
public static class NotificationNavigation
{
    /// <summary>Returns the route for the target, or null when the target type is unknown to this client.</summary>
    public static NotificationPageRoute? Resolve(string targetType, PermissionFlags flags)
    {
        bool canSeeCashPositions = flags.CanManageCashOperations || flags.CanApproveEndOfDay || flags.CanViewAudit;
        return targetType switch
        {
            NotificationTargets.SessionDetail => new(PageNames.SessionDetail, canSeeCashPositions),
            NotificationTargets.SessionManagement => new(PageNames.CashReconciliation, canSeeCashPositions),
            NotificationTargets.CashHandoff => new(PageNames.SessionHandoffs, canSeeCashPositions),
            NotificationTargets.CashRequest => new(PageNames.CashRequests, flags.CanApproveEndOfDay),
            NotificationTargets.BankExpense => new(PageNames.BankExpenses, flags.CanManageCashOperations || flags.CanApproveEndOfDay),
            NotificationTargets.ReconciliationTask => new(PageNames.AssignedReconciliationTasks, flags.CanInvestigateReconciliation),
            NotificationTargets.AccountProposal or NotificationTargets.CashProposal =>
                new(PageNames.ReconcileProposals, flags.CanApproveCorrections),
            NotificationTargets.AccountVerification or NotificationTargets.CashVerification =>
                new(PageNames.ReconciliationVerification, flags.CanInvestigateReconciliation || flags.CanApproveCorrections),
            NotificationTargets.ReconciliationChecks =>
                new(PageNames.ReconciliationMaster, flags.CanAccessAccounting || flags.CanApproveEndOfDay || flags.CanViewAudit),
            _ => null
        };
    }
}
