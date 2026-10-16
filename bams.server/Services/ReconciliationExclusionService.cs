using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using bams.server.Constants;
using bams.server.Data;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace bams.server.Services;

/// <summary>Keeps the exact financial source set behind a manager's audited Force Pass.</summary>
public sealed class ReconciliationExclusionService(ApplicationDbContext db, ICurrentUserService currentUser)
{
    private sealed record SourceRef(string Type, long Id, string State);

    public async Task ForcePassAsync(long exceptionId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5 || reason.Length > 2000)
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        long actor = currentUser.GetCurrentUserId();
        if (!await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
            join staff in db.Users on ur.UserId equals staff.Id
            where ur.UserId == actor && role.Code == SecurityConstants.ManagerRole && staff.Status == UserStatus.Active
            select ur.UserId).AnyAsync(ct))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        ReconciliationException exception = (await db.ReconciliationExceptions.FromSql($"SELECT * FROM ReconciliationExceptions WHERE Id = {exceptionId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (exception.Status is OperationsConstants.ExceptionResolved or OperationsConstants.ExceptionForcePassed)
            throw new ConflictException(MessageCode.Conflict);
        if (!await db.BusinessDates.AnyAsync(x => x.Date == exception.BusinessDate && x.Status == OperationsConstants.BusinessDateOpen, ct))
            throw new ConflictException(MessageCode.BusinessDateTransitionConflict);
        List<SourceRef> sources = await SnapshotAsync(exception, ct);
        if (sources.Count == 0) throw new BusinessRuleException(MessageCode.InvalidRequest);
        ReconciliationExclusion exclusion = new ReconciliationExclusion { ExceptionId = exception.Id, ManagerId = actor,
            Reason = reason.Trim(), SourceHash = Hash(sources),
            SourceIdsJson = JsonSerializer.Serialize(sources.Select(x => new { x.Type, x.Id })), CreatedAtUtc = DateTime.UtcNow };
        foreach (SourceRef source in sources.DistinctBy(x => (x.Type, x.Id)))
            exclusion.Sources.Add(new ReconciliationExcludedSource { SourceType = source.Type, SourceId = source.Id });
        db.ReconciliationExclusions.Add(exclusion);
        foreach (CorrectionProposal proposal in await db.CorrectionProposals.Where(x => x.ExceptionId == exceptionId && x.Status == ProposalStatus.PendingApproval).ToListAsync(ct))
            proposal.Status = ProposalStatus.Superseded;
        foreach (CashCorrectionProposal proposal in await db.CashCorrectionProposals.Where(x => x.ExceptionId == exceptionId && x.Status == ProposalStatus.PendingApproval).ToListAsync(ct))
            proposal.Status = ProposalStatus.Superseded;
        db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory { Exception = exception,
            OldStatus = exception.Status, NewStatus = OperationsConstants.ExceptionForcePassed,
            Note = $"Manager Force Pass: {reason.Trim()}", ActorId = actor, CreatedAtUtc = DateTime.UtcNow });
        exception.Status = OperationsConstants.ExceptionForcePassed;
        exception.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task ValidateAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        List<ReconciliationExclusion> exclusions = await db.ReconciliationExclusions.Include(x => x.Exception)
            .Where(x => x.Exception!.BusinessDate >= fromDate && x.Exception.BusinessDate <= toDate &&
                x.Exception.Status == OperationsConstants.ExceptionForcePassed &&
                !db.ReconciliationExclusions.Any(newer => newer.ExceptionId == x.ExceptionId && newer.Id > x.Id)).ToListAsync(ct);
        foreach (ReconciliationExclusion exclusion in exclusions)
        {
            if (Hash(await SnapshotAsync(exclusion.Exception!, ct)) == exclusion.SourceHash) continue;
            ReconciliationException exception = exclusion.Exception!;
            db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory { Exception = exception,
                OldStatus = OperationsConstants.ExceptionForcePassed, NewStatus = OperationsConstants.ExceptionOpen,
                Note = "Force Pass source changed; reconciliation must run again.",
                ActorId = currentUser.GetCurrentUserId(), CreatedAtUtc = DateTime.UtcNow });
            exception.Status = OperationsConstants.ExceptionOpen;
            exception.UpdatedAtUtc = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<HashSet<long>> ActiveSourceIdsAsync(DateOnly date, string sourceType, CancellationToken ct) =>
        (await db.ReconciliationExcludedSources.AsNoTracking().Where(x => x.SourceType == sourceType &&
            x.Exclusion!.Exception!.BusinessDate == date && x.Exclusion.Exception.Status == OperationsConstants.ExceptionForcePassed &&
            (x.Exclusion.Exception.Type == "JournalIntegrity" || x.Exclusion.Exception.Type == OperationsConstants.ExceptionLedgerBalance) &&
            !db.ReconciliationExclusions.Any(newer => newer.ExceptionId == x.Exclusion.ExceptionId && newer.Id > x.ExclusionId))
            .Select(x => x.SourceId).ToListAsync(ct)).ToHashSet();

    private async Task<List<SourceRef>> SnapshotAsync(ReconciliationException exception, CancellationToken ct)
    {
        List<SourceRef> refs = new();
        HashSet<long> transactionIds = new();
        if (exception.RelatedTransactionId.HasValue) transactionIds.Add(exception.RelatedTransactionId.Value);
        if (exception.AccountId.HasValue)
        {
            bams.server.Models.Accounts.Account? account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == exception.AccountId, ct);
            if (account is not null) refs.Add(new("Account", account.Id, $"{account.LedgerBalance}:{account.AvailableBalance}:{account.Version}"));
            List<bams.server.Models.Transactions.AccountTransaction> entries = await db.AccountTransactions.AsNoTracking().Where(x => x.AccountId == exception.AccountId && x.PostingDate <= exception.BusinessDate).ToListAsync(ct);
            foreach (bams.server.Models.Transactions.AccountTransaction entry in entries)
            {
                refs.Add(new("AccountEntry", entry.Id, $"{entry.TransactionId}:{entry.Amount}:{entry.EntryType}:{entry.PostingDate}"));
                transactionIds.Add(entry.TransactionId);
            }
        }
        if (exception.PositionSessionId.HasValue)
        {
            CashPositionSession? session = await db.CashPositionSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == exception.PositionSessionId, ct);
            if (session is not null) refs.Add(new("Session", session.Id, $"{session.ExpectedClosingCash}:{session.Version}:{session.Status}"));
            foreach (CashMovement movement in await db.CashMovements.AsNoTracking().Where(x => x.SessionId == exception.PositionSessionId || x.DestinationSessionId == exception.PositionSessionId).ToListAsync(ct))
            {
                refs.Add(new("Movement", movement.Id, $"{movement.Type}:{movement.Status}:{movement.Amount}:{movement.TransactionId}:{movement.DestinationSessionId}"));
                if (movement.TransactionId.HasValue) transactionIds.Add(movement.TransactionId.Value);
            }
            foreach (CashCount count in await db.CashCounts.AsNoTracking().Where(x => x.SessionId == exception.PositionSessionId).ToListAsync(ct))
                refs.Add(new("Count", count.Id, $"{count.ExpectedAmount}:{count.ActualAmount}:{count.CountedAtUtc.Ticks}"));
            foreach (CashHandoff handoff in await db.CashHandoffs.AsNoTracking().Where(x => x.SessionId == exception.PositionSessionId || x.DestinationSessionId == exception.PositionSessionId).ToListAsync(ct))
                refs.Add(new("Handoff", handoff.Id, $"{handoff.Status}:{handoff.Amount}:{handoff.Version}"));
        }
        if (!exception.AccountId.HasValue && !exception.PositionSessionId.HasValue && !exception.RelatedTransactionId.HasValue)
            foreach (long id in await db.TransactionEntries.AsNoTracking().Where(x => x.PostingDate == exception.BusinessDate)
                .Select(x => x.TransactionId).Distinct().ToListAsync(ct)) transactionIds.Add(id);
        if (transactionIds.Count > 0)
        {
            foreach (bams.server.Models.Transactions.Transaction transaction in await db.Transactions.AsNoTracking().Where(x => transactionIds.Contains(x.Id)).ToListAsync(ct))
                refs.Add(new("Transaction", transaction.Id, $"{transaction.TransactionStatus}:{transaction.Amount}:{transaction.BusinessDate}:{transaction.UpdatedAt.Ticks}"));
            foreach (bams.server.Models.Transactions.TransactionEntry line in await db.TransactionEntries.AsNoTracking().Where(x => transactionIds.Contains(x.TransactionId)).ToListAsync(ct))
                refs.Add(new("GL", line.Id, $"{line.TransactionId}:{line.GlAccountId}:{line.CustomerAccountId}:{line.EntryType}:{line.Amount}:{line.PostingDate}"));
        }
        return refs.OrderBy(x => x.Type).ThenBy(x => x.Id).ToList();
    }

    private static string Hash(List<SourceRef> refs) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(refs))));
}
