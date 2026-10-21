using bams.server.Constants;
using bams.server.Models.Accounting;
using Groups = bams.server.Constants.NotificationConstants.Groups;
using Targets = bams.server.Constants.NotificationConstants.Targets;
using Types = bams.server.Constants.NotificationConstants.Types;

namespace bams.server.Services.Notifications;

/// <summary>Outcome of a manager's review of a correction proposal.</summary>
public enum ProposalReviewOutcome
{
    Approved,
    Rejected,
    Superseded
}

/// <summary>A pending proposal that a Force Pass cancelled.</summary>
public sealed record SupersededProposal(long ProposalId, long ProposerId, bool IsCash);

/// <summary>
/// Notification text for reconciliation assignment, proposal review, verification and Force Pass.
/// Shared by the account/GL and cash workflows so both read the same way.
/// </summary>
public sealed class ReconciliationNotifier(INotificationService notifications)
{
    private const string Category = NotificationConstants.Categories.Reconciliation;

    /// <summary>Gives the exception to the new investigator and tells a replaced investigator.</summary>
    public async Task ExceptionAssignedAsync(ReconciliationException exception, long? previousAssignee, long actor, string? note, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.ExceptionAssigned(exception.Id), ct);
        var manager = await notifications.GetUserNameAsync(actor, ct);
        var noteText = string.IsNullOrWhiteSpace(note) ? string.Empty : $" Note: {note.Trim()}";
        await notifications.NotifyUsersAsync([exception.AssignedTo], actor, new NotificationMessage(
            Types.ExceptionAssigned, Category, "Reconciliation task assigned to you",
            $"{manager} assigned you {Describe(exception)}.{noteText}",
            Targets.ReconciliationTask, exception.Id, Groups.ExceptionAssigned(exception.Id)), ct);
        if (previousAssignee.HasValue && previousAssignee != exception.AssignedTo)
        {
            var investigator = await notifications.GetUserNameAsync(exception.AssignedTo, ct);
            await notifications.NotifyUsersAsync([previousAssignee], actor, new NotificationMessage(
                Types.ExceptionUnassigned, Category, "Reconciliation task reassigned",
                $"{manager} reassigned {Describe(exception)} to {investigator}.",
                Targets.ReconciliationChecks, exception.Id), ct);
        }
    }

    /// <summary>Asks every manager to review a submitted proposal; the copies disappear once one decides.</summary>
    public async Task ProposalSubmittedAsync(ReconciliationException exception, long proposalId, bool isCash, long actor, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.ExceptionAssigned(exception.Id), ct);
        var investigator = await notifications.GetUserNameAsync(actor, ct);
        await notifications.NotifyRoleAsync(SecurityConstants.ManagerRole, actor, new NotificationMessage(
            Types.ProposalSubmitted, Category, "Correction proposal needs review",
            $"{investigator} submitted {(isCash ? "cash" : "account")} correction proposal #{proposalId} for {Describe(exception)}.",
            isCash ? Targets.CashProposal : Targets.AccountProposal, proposalId, ReviewGroup(proposalId, isCash)), ct);
    }

    /// <summary>Tells the proposer and investigator what the manager decided.</summary>
    public async Task ProposalReviewedAsync(ReconciliationException exception, long proposalId, long proposerId, bool isCash,
        ProposalReviewOutcome outcome, string? note, long actor, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(ReviewGroup(proposalId, isCash), ct);
        var manager = await notifications.GetUserNameAsync(actor, ct);
        var (type, title, detail) = outcome switch
        {
            ProposalReviewOutcome.Approved => (Types.ProposalApproved, "Correction proposal approved",
                $"approved and posted by {manager}. Verification runs next."),
            ProposalReviewOutcome.Rejected => (Types.ProposalRejected, "Correction proposal rejected",
                $"rejected by {manager}. Note: {note}. The exception is back under investigation."),
            _ => (Types.ProposalSuperseded, "Correction proposal out of date",
                "not approved because its source records changed. Prepare a new proposal.")
        };
        await notifications.NotifyUsersAsync([proposerId, exception.AssignedTo], actor, new NotificationMessage(
            type, Category, title,
            $"Proposal #{proposalId} for {Describe(exception)} was {detail}",
            outcome != ProposalReviewOutcome.Approved ? Targets.ReconciliationTask
                : isCash ? Targets.CashVerification : Targets.AccountVerification,
            outcome == ProposalReviewOutcome.Approved ? proposalId : exception.Id), ct);
    }

    /// <summary>Sends the exception back to the investigator after a failed verification.</summary>
    public async Task VerificationFailedAsync(ReconciliationException exception, long proposalId, long proposerId, string? note, long actor, CancellationToken ct)
    {
        await notifications.NotifyUsersAsync([proposerId, exception.AssignedTo], actor, new NotificationMessage(
            Types.VerificationFailed, Category, "Correction failed verification",
            $"The correction from proposal #{proposalId} for {Describe(exception)} did not pass verification. {note} The exception was sent back for investigation.",
            Targets.ReconciliationTask, exception.Id), ct);
    }

    /// <summary>Tells the investigator and every cancelled proposer that a manager closed the exception.</summary>
    public async Task ExceptionForcePassedAsync(ReconciliationException exception, IReadOnlyList<SupersededProposal> superseded,
        string reason, long actor, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.ExceptionAssigned(exception.Id), ct);
        foreach (var proposal in superseded)
            await notifications.DismissGroupAsync(ReviewGroup(proposal.ProposalId, proposal.IsCash), ct);
        var manager = await notifications.GetUserNameAsync(actor, ct);
        var cancelled = superseded.Count == 0 ? string.Empty : " Pending correction proposals for it were cancelled.";
        await notifications.NotifyUsersAsync(superseded.Select(item => (long?)item.ProposerId).Append(exception.AssignedTo), actor,
            new NotificationMessage(Types.ExceptionForcePassed, Category, "Exception force-passed",
                $"{manager} force-passed {Describe(exception)}. Reason: {reason}.{cancelled}",
                Targets.ReconciliationChecks, exception.Id), ct);
    }

    private static string ReviewGroup(long proposalId, bool isCash) =>
        isCash ? Groups.CashProposalReview(proposalId) : Groups.AccountProposalReview(proposalId);

    private static string Describe(ReconciliationException exception) =>
        $"exception #{exception.Id} ({exception.Type}, {exception.BusinessDate:yyyy-MM-dd}, difference {exception.Difference:N2})";
}
