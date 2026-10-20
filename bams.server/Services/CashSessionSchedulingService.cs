using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Security;
using bams.server.Services.Notifications;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Creates schedules inside the caller's transaction, serializing reservations on the vault.</summary>
public sealed class CashSessionSchedulingService(ApplicationDbContext db, INotificationService notifications)
{
    /// <summary>Tells the officer a manager scheduled a teller session that they must accept.</summary>
    public async Task NotifySessionScheduledAsync(CashPositionSession session, long actorId, decimal startingCash, CancellationToken cancellationToken)
    {
        var manager = await notifications.GetUserNameAsync(actorId, cancellationToken);
        await notifications.NotifyUsersAsync([session.TellerId], actorId, new NotificationMessage(
            NotificationConstants.Types.SessionScheduled, NotificationConstants.Categories.CashManagement,
            "Cash session assigned to you",
            $"{manager} scheduled your teller session for {session.BusinessDate:yyyy-MM-dd} {FormatSessionWindow(session)} " +
            $"with {startingCash:N2} starting cash. Accept it to start working.",
            NotificationConstants.Targets.SessionDetail, session.Id,
            NotificationConstants.Groups.SessionScheduled(session.Id)), cancellationToken);
    }

    /// <summary>Formats the scheduled Myanmar time window for notification text.</summary>
    public static string FormatSessionWindow(CashPositionSession session) =>
        session.ScheduledStartAtMyanmar.HasValue && session.ScheduledEndAtMyanmar.HasValue
            ? $"{session.ScheduledStartAtMyanmar:HH:mm}-{session.ScheduledEndAtMyanmar:HH:mm}"
            : string.Empty;

    /// <summary>Validates the complete batch before reserving cash and creating its sessions.</summary>
    public async Task<IReadOnlyList<CashPositionSession>> CreateAsync(
        IReadOnlyList<ScheduledCashSessionRequest> rows, CashPositionSession vault, long actorId,
        string batchKey, CancellationToken cancellationToken)
    {
        if (vault.BusinessDate != BusinessTime.Today || vault.Status != OperationsConstants.CashSessionOpen)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        CashScheduleValidation.Validate(rows, vault.BusinessDate);
        var ids = rows.Select(row => row.OfficerId).Distinct().OrderBy(id => id).ToList();
        var officers = await (from user in db.Users join ur in db.UserRoles on user.Id equals ur.UserId
            join role in db.Roles on ur.RoleId equals role.Id
            where ids.Contains(user.Id) && user.Status == UserStatus.Active && role.Code == SecurityConstants.OfficerRole
            select user.Id).Distinct().ToListAsync(cancellationToken);
        var existing = await db.CashPositionSessions.Where(session => session.BusinessDate == vault.BusinessDate &&
            ids.Contains(session.TellerId ?? 0) && session.Status != OperationsConstants.CashSessionCancelled &&
            session.ScheduledStartAtMyanmar != null).ToListAsync(cancellationToken);
        decimal total = 0m;
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            if (!officers.Contains(row.OfficerId)) throw new ValidationException(MessageCode.InvalidRequest);
            if (existing.Any(session => session.TellerId == row.OfficerId && session.ScheduledStartAtMyanmar < row.EndAtMyanmar &&
                    session.ScheduledEndAtMyanmar > row.StartAtMyanmar))
                throw new ConflictException(MessageCode.CashSessionAlreadyExists);
            total += row.StartingCash;
        }
        if (total > vault.ExpectedClosingCash - vault.ReservedCash)
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
        var now = DateTime.UtcNow;
        List<CashPositionSession> created = [];
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var session = new CashPositionSession
            {
                PositionType = OperationsConstants.CashPositionTeller, TellerId = row.OfficerId,
                BusinessDate = vault.BusinessDate, ScheduledStartAtMyanmar = row.StartAtMyanmar,
                ScheduledEndAtMyanmar = row.EndAtMyanmar, Status = OperationsConstants.CashSessionScheduled,
                OpenedAtUtc = now, OpenedBy = actorId, OpenIdempotencyKey = $"{batchKey}:{index}"
            };
            db.CashPositionSessions.Add(session);
            db.CashProposals.Add(new CashProposal
            {
                Session = session, VaultSession = vault, OfficerId = row.OfficerId,
                RequestedAmount = row.StartingCash, ApprovedAmount = row.StartingCash, IsInitialCash = true,
                Status = "PendingReceipt", Note = "Manager-approved starting cash", CreatedAtUtc = now,
                ApprovedAtUtc = now, ApprovedBy = actorId
            });
            created.Add(session);
        }
        vault.ReservedCash += total;
        vault.Version++;
        await db.SaveChangesAsync(cancellationToken);

        // Session ids exist only after the save; the caller's transaction covers both saves.
        for (int index = 0; index < created.Count; index++)
            await NotifySessionScheduledAsync(created[index], actorId, rows[index].StartingCash, cancellationToken);
        if (created.Count > 0) await db.SaveChangesAsync(cancellationToken);
        return created;
    }
}
