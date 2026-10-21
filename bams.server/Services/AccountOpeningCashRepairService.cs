using System.Globalization;
using System.Text.Json;
using bams.server.Commands;
using bams.server.Constants;
using bams.server.Data;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Audit;
using bams.server.Models.Security;
using bams.server.Models.Transactions;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Repairs missing cash custody records without reposting customer balances or accounting journals.</summary>
public sealed class AccountOpeningCashRepairService(ApplicationDbContext db, LedgerPostingService ledger)
{
    /// <summary>Reports every candidate and applies independently atomic repairs only when explicitly requested.</summary>
    public async Task<int> RunAsync(AccountOpeningCashRepairCommand command, TextWriter output, CancellationToken ct)
    {
        if (command.Apply)
        {
            var manager = await db.Users.AsNoTracking().AnyAsync(user => user.Id == command.ActorId &&
                user.Status == UserStatus.Active && user.UserRoles.Any(role => role.Role!.Code == SecurityConstants.ManagerRole), ct);
            if (!manager) throw new ForbiddenException(MessageCode.InsufficientPermission);
        }

        var query = db.Transactions.AsNoTracking().Where(transaction =>
            transaction.Description == AccountConstants.OpeningDepositDescription &&
            transaction.TransactionType == TransactionType.CashDeposit &&
            transaction.TransactionStatus == TransactionStatus.Completed && transaction.Amount > 0m &&
            transaction.AccountingRootTransactionId == null && transaction.ReversalOfTransactionId == null);
        if (command.TransactionId.HasValue) query = query.Where(transaction => transaction.Id == command.TransactionId);
        var ids = await query.OrderBy(transaction => transaction.Id).Select(transaction => transaction.Id).ToListAsync(ct);
        await output.WriteLineAsync(command.Apply ? "APPLY: account-opening-cash" : "PREVIEW: account-opening-cash (no changes)");
        var repaired = 0;
        var blocked = 0;
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            try
            {
                var result = await ProcessAsync(id, command, ct);
                if (result.Status == "Repaired") repaired++;
                if (result.Status != "AlreadyLinked" && result.Status != "Ready" && result.Status != "Repaired") blocked++;
                await output.WriteLineAsync(JsonSerializer.Serialize(result));
            }
            catch (AppException exception)
            {
                blocked++;
                await output.WriteLineAsync(JsonSerializer.Serialize(new { TransactionId = id, Status = "Failed", Code = (int)exception.Code, exception.Message }));
            }
        }
        await output.WriteLineAsync($"Candidates={ids.Count}; Repaired={repaired}; NeedsReview={blocked}");
        // Nonzero means at least one candidate needs manual review, not that already committed repairs were undone.
        return blocked > 0 || (command.TransactionId.HasValue && ids.Count == 0) ? 1 : 0;
    }

    private sealed record RepairResult(long TransactionId, string Status, long? SessionId = null,
        decimal? Amount = null, decimal? ExpectedBefore = null, decimal? ExpectedAfter = null);

    // Locks the original business date and teller position before rechecking links, including other correction workflows.
    private async Task<RepairResult> ProcessAsync(long id, AccountOpeningCashRepairCommand command, CancellationToken ct)
    {
        await using var transaction = command.Apply ? await db.Database.BeginTransactionAsync(ct) : null;
        var source = await db.Transactions.AsNoTracking().SingleAsync(item => item.Id == id, ct);
        var entries = await db.AccountTransactions.AsNoTracking().Include(entry => entry.Account)
            .Where(entry => entry.TransactionId == id).ToListAsync(ct);
        if (entries.Count != 1 || entries[0].EntryType != EntryType.Credit || entries[0].Amount != source.Amount ||
            entries[0].Account?.AccountNo != source.ReferenceNo || entries[0].Account?.OpenedAt != source.TransactionAt || source.FeeAmount != 0m)
            return new(id, "OpeningEvidenceMismatch");
        var postingDate = source.BusinessDate ?? entries[0].PostingDate;
        if (postingDate != entries[0].PostingDate) return new(id, "PostingDateMismatch");
        var postedAt = source.PostedAt ?? source.TransactionAt;
        if (command.Apply)
            await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Date = {postingDate} FOR UPDATE").ToListAsync(ct);

        // Even correction and adjustment movements count as an existing link: never credit the same deposit twice.
        if (await HasMovementAsync(id, ct)) return new(id, "AlreadyLinked");
        if (await IsExcludedAsync(id, command.Apply, ct))
            return new(id, "ExcludedByReconciliation");
        if (await HasPendingCorrectionAsync(id, ct)) return new(id, "PendingCorrection");
        var sessions = await GetMatchingSessionsAsync(source.InitiatedBy, postingDate, postedAt, ct);
        if (sessions.Count != 1) return new(id, sessions.Count == 0 ? "NoMatchingSession" : "AmbiguousSession");
        var session = sessions[0];
        if (command.Apply)
        {
            session = (await db.CashPositionSessions.FromSql($"SELECT * FROM CashPositionSessions WHERE Id = {session.Id} FOR UPDATE")
                .ToListAsync(ct)).Single();
            // A concurrent repair/correction must be seen after acquiring its session lock (locking read in MySQL).
            var links = await db.CashMovements.FromSql($"SELECT * FROM CashMovements WHERE TransactionId = {id} OR CorrectionTransactionId = {id} FOR UPDATE").ToListAsync(ct);
            if (links.Count > 0) return new(id, "AlreadyLinked");
            source = (await db.Transactions.FromSql($"SELECT * FROM Transactions WHERE Id = {id} FOR UPDATE").ToListAsync(ct)).Single();
            if (source.TransactionStatus != TransactionStatus.Completed || source.Amount != entries[0].Amount ||
                source.TransactionType != TransactionType.CashDeposit || source.Description != AccountConstants.OpeningDepositDescription ||
                (source.PostedAt ?? source.TransactionAt) != postedAt || source.ReferenceNo != entries[0].Account?.AccountNo ||
                source.FeeAmount != 0m || source.BusinessDate.HasValue && source.BusinessDate != postingDate ||
                session.TellerId != source.InitiatedBy || session.BusinessDate != postingDate ||
                session.ActivatedAtUtc is null || session.ActivatedAtUtc > postedAt ||
                session.Status == OperationsConstants.CashSessionClosed && (session.ClosedAtUtc is null || session.ClosedAtUtc < postedAt) ||
                session.Status == OperationsConstants.CashSessionOpen && session.ClosedAtUtc is not null ||
                session.PositionType != OperationsConstants.CashPositionTeller)
                return new(id, "SourceChanged");
        }
        if (session.Status != OperationsConstants.CashSessionOpen && session.Status != OperationsConstants.CashSessionClosed)
            return new(id, "SourceChanged");
        if (session.Status == OperationsConstants.CashSessionClosed && !command.IncludeClosed)
            return new(id, "ClosedSessionRequiresFlag", session.Id, source.Amount);
        await ledger.ValidateTransactionAccountingEntriesAsync(id, ct);
        var before = session.ExpectedClosingCash;
        if (!command.Apply) return new(id, "Ready", session.Id, source.Amount, before, before + source.Amount);

        await RecordRepairAsync(source, session, command, postedAt, ct);
        await transaction!.CommitAsync(ct);
        return new(id, "Repaired", session.Id, source.Amount, before, session.ExpectedClosingCash);
    }

    // Historical time windows prevent deposits from being assigned to a newer session on the same day.
    private Task<List<CashPositionSession>> GetMatchingSessionsAsync(long tellerId, DateOnly postingDate, DateTime postedAt, CancellationToken ct) =>
        db.CashPositionSessions.AsNoTracking().Where(session =>
            session.PositionType == OperationsConstants.CashPositionTeller && session.TellerId == tellerId &&
            session.BusinessDate == postingDate && session.ActivatedAtUtc != null && session.ActivatedAtUtc <= postedAt &&
            (session.Status == OperationsConstants.CashSessionOpen && session.ClosedAtUtc == null ||
             session.Status == OperationsConstants.CashSessionClosed && session.ClosedAtUtc >= postedAt)).ToListAsync(ct);

    // Appends the missing custody record and its audit attribution without touching financial records.
    private async Task RecordRepairAsync(Transaction source, CashPositionSession session,
        AccountOpeningCashRepairCommand command, DateTime postedAt, CancellationToken ct)
    {
        var before = session.ExpectedClosingCash;

        var now = DateTime.UtcNow;
        db.CashMovements.Add(new CashMovement
        {
            SessionId = session.Id, TransactionId = source.Id, Type = "Deposit", Amount = source.Amount,
            ActorId = source.InitiatedBy, ApprovedBy = command.ActorId, ApprovedAtUtc = now, CreatedAtUtc = now,
            Note = $"Account-opening cash repair; original posting {postedAt:O}; operator {command.ActorId}."
        });
        session.ExpectedClosingCash += source.Amount;
        session.Version++;
        db.AuditLogs.Add(new AuditLog
        {
            UserId = command.ActorId!.Value, Action = AuditConstants.AccountOpeningCashRepairAction,
            EntityType = AuditConstants.TransactionEntityType, EntityId = source.Id.ToString(CultureInfo.InvariantCulture),
            OldValues = JsonSerializer.Serialize(new { SessionId = session.Id, ExpectedClosingCash = before }),
            NewValues = JsonSerializer.Serialize(new { SessionId = session.Id, session.ExpectedClosingCash, source.Amount, command.IncludeClosed }),
            DeviceInfo = AccountOpeningCashRepairCommand.Switch, CreatedAt = now
        });
        await db.SaveChangesAsync(ct);
    }

    // Apply uses a current locking read so a waiver committed while waiting for the date lock is not missed.
    private async Task<bool> IsExcludedAsync(long id, bool lockRecords, CancellationToken ct)
    {
        if (lockRecords)
            return (await db.ReconciliationExcludedSources.FromSql(
                $"SELECT * FROM ReconciliationExcludedSources WHERE SourceType = 'Transaction' AND SourceId = {id} FOR UPDATE").ToListAsync(ct)).Count > 0;
        return await db.ReconciliationExcludedSources.AsNoTracking()
            .AnyAsync(item => item.SourceType == "Transaction" && item.SourceId == id, ct);
    }

    // Treats any existing movement reference as evidence requiring review rather than creating another movement.
    private Task<bool> HasMovementAsync(long id, CancellationToken ct) => db.CashMovements.AsNoTracking()
        .AnyAsync(movement => movement.TransactionId == id || movement.CorrectionTransactionId == id, ct);

    // Existing investigations must be resolved through their own approval workflow rather than silently bypassed.
    private async Task<bool> HasPendingCorrectionAsync(long id, CancellationToken ct)
    {
        var proposals = await db.CashCorrectionProposals.AsNoTracking()
            .Where(proposal => proposal.Kind == CashDiscrepancyKind.MissingMovement && proposal.Status == ProposalStatus.PendingApproval)
            .Select(proposal => proposal.InputsJson).ToListAsync(ct);
        foreach (var json in proposals)
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("TransactionId", out var value) &&
                value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var transactionId) && transactionId == id) return true;
        }
        return false;
    }
}
