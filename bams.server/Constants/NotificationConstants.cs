namespace bams.server.Constants;

/// <summary>Stable values stored on notification rows and read by the desktop client.</summary>
public static class NotificationConstants
{
    public const int MaximumTitleLength = 160;
    public const int MaximumMessageLength = 1000;
    public const int MaximumUnreadReturned = 100;

    public static class Categories
    {
        public const string CashManagement = "Cash Management";
        public const string Reconciliation = "Reconciliation";
    }

    // Desktop maps each target to a page; keep these in sync with bams.desktop NotificationTargets.
    public static class Targets
    {
        public const string SessionDetail = "SessionDetail";
        public const string SessionManagement = "SessionManagement";
        public const string CashRequest = "CashRequest";
        public const string CashHandoff = "CashHandoff";
        public const string BankExpense = "BankExpense";
        public const string ReconciliationTask = "ReconciliationTask";
        public const string AccountProposal = "AccountProposal";
        public const string CashProposal = "CashProposal";
        // Account and cash proposals have separate id sequences, so verification targets keep the kind.
        public const string AccountVerification = "AccountVerification";
        public const string CashVerification = "CashVerification";
        public const string ReconciliationChecks = "ReconciliationChecks";
    }

    public static class Types
    {
        public const string SessionScheduled = "SessionScheduled";
        public const string SessionAccepted = "SessionAccepted";
        public const string SessionCancelled = "SessionCancelled";
        public const string SessionExpired = "SessionExpired";
        public const string CashRequestSubmitted = "CashRequestSubmitted";
        public const string CashRequestWithdrawn = "CashRequestWithdrawn";
        public const string CashRequestApproved = "CashRequestApproved";
        public const string CashRequestRejected = "CashRequestRejected";
        public const string CashReceived = "CashReceived";
        public const string CashRefused = "CashRefused";
        public const string HandoffReceived = "HandoffReceived";
        public const string HandoffAccepted = "HandoffAccepted";
        public const string HandoffDeclined = "HandoffDeclined";
        public const string HandoffReassigned = "HandoffReassigned";
        public const string BankExpenseSubmitted = "BankExpenseSubmitted";
        public const string BankExpenseApproved = "BankExpenseApproved";
        public const string BankExpenseRejected = "BankExpenseRejected";
        public const string ExceptionAssigned = "ExceptionAssigned";
        public const string ExceptionUnassigned = "ExceptionUnassigned";
        public const string ProposalSubmitted = "ProposalSubmitted";
        public const string ProposalApproved = "ProposalApproved";
        public const string ProposalRejected = "ProposalRejected";
        public const string ProposalSuperseded = "ProposalSuperseded";
        public const string VerificationFailed = "VerificationFailed";
        public const string ExceptionForcePassed = "ExceptionForcePassed";
    }

    /// <summary>Group keys tie every copy of one pending task together.</summary>
    public static class Groups
    {
        public static string SessionScheduled(long sessionId) => $"session-scheduled:{sessionId}";
        public static string CashRequestReview(long proposalId) => $"cash-request-review:{proposalId}";
        public static string CashReceipt(long proposalId) => $"cash-receipt:{proposalId}";
        public static string HandoffPending(long handoffId) => $"handoff-pending:{handoffId}";
        public static string HandoffDeclined(long handoffId) => $"handoff-declined:{handoffId}";
        public static string BankExpenseReview(long expenseId) => $"bank-expense-review:{expenseId}";
        public static string ExceptionAssigned(long exceptionId) => $"exception-assigned:{exceptionId}";
        public static string AccountProposalReview(long proposalId) => $"account-proposal-review:{proposalId}";
        public static string CashProposalReview(long proposalId) => $"cash-proposal-review:{proposalId}";
    }
}
