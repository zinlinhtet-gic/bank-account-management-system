using bams.server.Constants;
using bams.server.Models.Accounting;
using bams.server.Services.Notifications;
using Groups = bams.server.Constants.NotificationConstants.Groups;
using Targets = bams.server.Constants.NotificationConstants.Targets;
using Types = bams.server.Constants.NotificationConstants.Types;

namespace bams.server.Services;

/// <summary>Notification side effects of cash session, cash request and handoff actions.</summary>
public sealed partial class CashOperationsService
{
    private const string CashCategory = NotificationConstants.Categories.CashManagement;
    private static readonly string[] CashRequestReviewerPermissions = [SecurityConstants.EndOfDayApproval];
    private static readonly string[] HandoffReviewerPermissions = [SecurityConstants.Audit, SecurityConstants.EndOfDayApproval];

    // Tells the scheduling manager the officer accepted the session and took the starting cash.
    private async Task NotifySessionAcceptedAsync(CashPositionSession session, long actorId, decimal startingCash, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.SessionScheduled(session.Id), ct);
        var officer = await notifications.GetUserNameAsync(actorId, ct);
        await notifications.NotifyUsersAsync([session.OpenedBy], actorId, new NotificationMessage(
            Types.SessionAccepted, CashCategory, "Cash session accepted",
            $"{officer} accepted teller session #{session.Id} and received {startingCash:N2} starting cash.",
            Targets.SessionDetail, session.Id), ct);
    }

    // Tells the officer their scheduled session was cancelled before they accepted it.
    private async Task NotifySessionCancelledAsync(CashPositionSession session, long actorId, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.SessionScheduled(session.Id), ct);
        var manager = await notifications.GetUserNameAsync(actorId, ct);
        await notifications.NotifyUsersAsync([session.TellerId], actorId, new NotificationMessage(
            Types.SessionCancelled, CashCategory, "Cash session cancelled",
            $"{manager} cancelled your scheduled teller session for {session.BusinessDate:yyyy-MM-dd} {CashSessionSchedulingService.FormatSessionWindow(session)}.",
            Targets.SessionManagement, session.Id), ct);
    }

    // Asks every vault manager to review a new cash request; the copies disappear once one acts.
    private async Task NotifyCashRequestSubmittedAsync(CashProposal proposal, long actorId, CancellationToken ct)
    {
        var officer = await notifications.GetUserNameAsync(actorId, ct);
        var amount = proposal.RequestedAmount.HasValue ? $"{proposal.RequestedAmount.Value:N2}" : "additional cash";
        await notifications.NotifyPermissionHoldersAsync(CashRequestReviewerPermissions, actorId, new NotificationMessage(
            Types.CashRequestSubmitted, CashCategory, "Cash request needs review",
            $"{officer} requested {amount} from the vault (request #{proposal.Id}). Note: {proposal.Note}",
            Targets.CashRequest, proposal.Id, Groups.CashRequestReview(proposal.Id)), ct);
    }

    // Tells the officer the manager approved the request and the cash is waiting for their receipt.
    private async Task NotifyCashRequestApprovedAsync(CashProposal proposal, long actorId, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.CashRequestReview(proposal.Id), ct);
        var manager = await notifications.GetUserNameAsync(actorId, ct);
        await notifications.NotifyUsersAsync([proposal.OfficerId], actorId, new NotificationMessage(
            Types.CashRequestApproved, CashCategory, "Cash request approved",
            $"{manager} approved {proposal.ApprovedAmount:N2} for cash request #{proposal.Id}. Receive the cash to add it to your session.",
            Targets.SessionDetail, proposal.SessionId, Groups.CashReceipt(proposal.Id)), ct);
    }

    // A pending request was rejected by a manager or withdrawn by its officer.
    private async Task NotifyCashRequestRejectedAsync(CashProposal proposal, long actorId, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.CashRequestReview(proposal.Id), ct);
        if (proposal.OfficerId == actorId) return; // Withdrawn by the officer: dismissing the manager copies is enough.
        var manager = await notifications.GetUserNameAsync(actorId, ct);
        await notifications.NotifyUsersAsync([proposal.OfficerId], actorId, new NotificationMessage(
            Types.CashRequestRejected, CashCategory, "Cash request rejected",
            $"{manager} rejected cash request #{proposal.Id}.",
            Targets.SessionDetail, proposal.SessionId), ct);
    }

    // Tells the approving manager whether the officer signed for or refused the approved cash.
    private async Task NotifyCashReceiptResolvedAsync(CashProposal proposal, long actorId, bool received, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.CashReceipt(proposal.Id), ct);
        var officer = await notifications.GetUserNameAsync(actorId, ct);
        await notifications.NotifyUsersAsync([proposal.ApprovedBy], actorId, new NotificationMessage(
            received ? Types.CashReceived : Types.CashRefused, CashCategory,
            received ? "Approved cash received" : "Approved cash refused",
            received
                ? $"{officer} received {proposal.ApprovedAmount:N2} for cash request #{proposal.Id}."
                : $"{officer} refused {proposal.ApprovedAmount:N2} for cash request #{proposal.Id}; the reserved cash was returned to the vault.",
            Targets.CashRequest, proposal.Id), ct);
    }

    // Asks the chosen recipient to accept or decline cash handed over when a session closed.
    private async Task NotifyHandoffReceivedAsync(CashHandoff handoff, long actorId, CancellationToken ct)
    {
        var sender = await notifications.GetUserNameAsync(handoff.SenderId, ct);
        await notifications.NotifyUsersAsync([handoff.RecipientId], actorId, new NotificationMessage(
            Types.HandoffReceived, CashCategory, "Cash handoff waiting for you",
            $"{sender} handed you {handoff.Amount:N2} from session #{handoff.SessionId}. Accept it once you have the cash, or decline it.",
            Targets.CashHandoff, handoff.Id, Groups.HandoffPending(handoff.Id)), ct);
    }

    // Tells the sender the outcome; a decline also reaches everyone who can reassign it.
    private async Task NotifyHandoffDecidedAsync(CashHandoff handoff, long actorId, bool accepted, string? note, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.HandoffPending(handoff.Id), ct);
        var recipient = await notifications.GetUserNameAsync(actorId, ct);
        if (accepted)
        {
            await notifications.NotifyUsersAsync([handoff.SenderId], actorId, new NotificationMessage(
                Types.HandoffAccepted, CashCategory, "Cash handoff accepted",
                $"{recipient} accepted {handoff.Amount:N2} from session #{handoff.SessionId}.",
                Targets.CashHandoff, handoff.Id), ct);
            return;
        }

        // A declined handoff blocks End of Day, so reviewers who can reassign it are told too.
        var reviewers = await notifications.GetPermissionHolderIdsAsync(HandoffReviewerPermissions, ct);
        await notifications.NotifyUsersAsync(reviewers.Select(id => (long?)id).Append(handoff.SenderId), actorId,
            new NotificationMessage(Types.HandoffDeclined, CashCategory, "Cash handoff declined",
                $"{recipient} declined {handoff.Amount:N2} from session #{handoff.SessionId}. Reason: {note}. Reassign it to another recipient.",
                Targets.CashHandoff, handoff.Id, Groups.HandoffDeclined(handoff.Id)), ct);
    }

    // Moves the pending task to the new recipient and tells the people who lost or own it.
    private async Task NotifyHandoffReassignedAsync(CashHandoff handoff, long previousRecipientId, long actorId, CancellationToken ct)
    {
        await notifications.DismissGroupAsync(Groups.HandoffPending(handoff.Id), ct);
        await notifications.DismissGroupAsync(Groups.HandoffDeclined(handoff.Id), ct);
        await NotifyHandoffReceivedAsync(handoff, actorId, ct);
        var actor = await notifications.GetUserNameAsync(actorId, ct);
        var newRecipient = await notifications.GetUserNameAsync(handoff.RecipientId, ct);
        var others = new List<long?> { handoff.SenderId };
        if (previousRecipientId != handoff.RecipientId) others.Add(previousRecipientId);
        await notifications.NotifyUsersAsync(others.Where(id => id != handoff.RecipientId), actorId, new NotificationMessage(
            Types.HandoffReassigned, CashCategory, "Cash handoff reassigned",
            $"{actor} reassigned the {handoff.Amount:N2} handoff from session #{handoff.SessionId} to {newRecipient}.",
            Targets.CashHandoff, handoff.Id), ct);
    }
}
