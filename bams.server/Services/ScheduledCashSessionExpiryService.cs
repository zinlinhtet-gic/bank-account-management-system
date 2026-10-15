using bams.server.Constants;
using bams.server.Data;
using bams.server.Services.Jobs;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Closes teller sessions that were never activated before their scheduled Myanmar end time.</summary>
public sealed class ScheduledCashSessionExpiryService(ApplicationDbContext db)
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
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
