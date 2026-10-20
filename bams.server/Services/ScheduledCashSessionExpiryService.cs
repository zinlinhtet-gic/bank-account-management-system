using bams.server.Constants;
using bams.server.Data;
using bams.server.Models.Accounting;
using bams.server.Services.Jobs;
using bams.server.Services.Notifications;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Closes teller sessions that were never activated before their scheduled Myanmar end time.</summary>
public sealed class ScheduledCashSessionExpiryService(ApplicationDbContext db, INotificationService notifications)
{
    public async Task ExecuteAsync(ScheduledJobExecutionContext execution, CancellationToken cancellationToken)
    {
        _ = execution;
        var nowUtc = DateTime.UtcNow;
        var nowMyanmar = BusinessTime.ToBusinessDateTime(nowUtc);
        var expiredIds = await db.CashPositionSessions.AsNoTracking()
            .Where(session => session.PositionType == OperationsConstants.CashPositionTeller &&
                session.Status == OperationsConstants.CashSessionScheduled && session.ScheduledEndAtMyanmar <= nowMyanmar)
            .Select(session => session.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in expiredIds)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var date = await db.CashPositionSessions.AsNoTracking().Where(item => item.Id == id).Select(item => item.BusinessDate).SingleAsync(cancellationToken);
            await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Date = {date} FOR UPDATE").ToListAsync(cancellationToken);
            var session = (await db.CashPositionSessions
                .FromSql($"SELECT * FROM CashPositionSessions WHERE Id = {id} FOR UPDATE")
                .ToListAsync(cancellationToken)).SingleOrDefault();
            if (session is null || session.PositionType != OperationsConstants.CashPositionTeller ||
                session.Status != OperationsConstants.CashSessionScheduled || session.ScheduledEndAtMyanmar > nowMyanmar)
            {
                await transaction.RollbackAsync(cancellationToken);
                continue;
            }

            var initialCashProposal = await db.CashProposals.SingleOrDefaultAsync(proposal =>
                proposal.SessionId == session.Id && proposal.IsInitialCash && proposal.Status == "PendingReceipt",
                cancellationToken);
            if (initialCashProposal is not null)
            {
                var vault = (await db.CashPositionSessions
                    .FromSql($"SELECT * FROM CashPositionSessions WHERE Id = {initialCashProposal.VaultSessionId} FOR UPDATE")
                    .ToListAsync(cancellationToken)).SingleOrDefault();
                decimal reservedAmount = initialCashProposal.ApprovedAmount ?? 0m;
                if (vault is null || vault.ReservedCash < reservedAmount)
                    throw new InvalidOperationException("The reserved initial cash for an expired teller session is missing from its vault session.");

                vault.ReservedCash -= reservedAmount;
                vault.Version++;
                initialCashProposal.Status = "Rejected";
                initialCashProposal.ResolvedAtUtc = nowUtc;
                initialCashProposal.Version++;
            }

            session.Status = OperationsConstants.CashSessionClosed;
            session.ClosedAtUtc = nowUtc;
            session.ClosedBy = null; // System expiry has no human actor.
            session.Version++;
            await NotifySessionExpiredAsync(session, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }

    // Tells the officer and the scheduling manager that the unaccepted session expired.
    private async Task NotifySessionExpiredAsync(CashPositionSession session, CancellationToken cancellationToken)
    {
        await notifications.DismissGroupAsync(NotificationConstants.Groups.SessionScheduled(session.Id), cancellationToken);
        var officer = await notifications.GetUserNameAsync(session.TellerId, cancellationToken);
        await notifications.NotifyUsersAsync([session.TellerId, session.OpenedBy], null, new NotificationMessage(
            NotificationConstants.Types.SessionExpired, NotificationConstants.Categories.CashManagement,
            "Cash session expired",
            $"Teller session #{session.Id} for {officer} on {session.BusinessDate:yyyy-MM-dd} " +
            $"{CashSessionSchedulingService.FormatSessionWindow(session)} expired because it was not accepted in time. " +
            "The reserved starting cash was returned to the vault.",
            NotificationConstants.Targets.SessionManagement, session.Id), cancellationToken);
    }
}
