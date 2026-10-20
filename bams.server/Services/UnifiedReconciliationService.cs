using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Security;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;

namespace bams.server.Services;

/// <summary>Runs the same account and cash checks for the master page and End of Day.</summary>
public sealed partial class UnifiedReconciliationService(ApplicationDbContext db, ReconciliationCheckService accounts,
    ReconciliationExclusionService exclusions, ICurrentUserService currentUser,
    IAccountReconciliationService reports, Notifications.ReconciliationNotifier notifier)
{
    public async Task<ReconciliationExceptionDetailResponse> DetailAsync(long id, CancellationToken ct)
    {
        long actor = currentUser.GetCurrentUserId();
        bool assigned = await db.ReconciliationExceptions.AnyAsync(x => x.Id == id && x.AssignedTo == actor, ct);
        bool accounting = await (from ur in db.UserRoles join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == actor && (p.Code == SecurityConstants.Accounting || p.Code == SecurityConstants.EndOfDayApproval)
            select ur.UserId).AnyAsync(ct);
        if (!assigned && !accounting) throw new ForbiddenException(MessageCode.InsufficientPermission);
        return await reports.GetExceptionByIdAsync(id, ct);
    }

    public Task<IReadOnlyList<ReconciliationAccountOptionResponse>> AccountOptionsAsync(string? search, CancellationToken ct) =>
        reports.SearchAccountsAsync(search, ct);

    public async Task<IReadOnlyList<ReconciliationStaffOptionResponse>> StaffAsync(CancellationToken ct) =>
        await (from user in db.Users join ur in db.UserRoles on user.Id equals ur.UserId
            join role in db.Roles on ur.RoleId equals role.Id
            where user.Status == UserStatus.Active && (role.Code == SecurityConstants.OfficerRole || role.Code == SecurityConstants.AuditorRole)
            orderby user.FullName
            select new ReconciliationStaffOptionResponse(user.Id,user.FullName,user.Username,role.Name)).Distinct().ToListAsync(ct);

    public async Task<UnifiedReconciliationResponse> RunAsync(UnifiedReconciliationRequest request, CancellationToken ct)
    {
        if (request.FromDate > request.ToDate || request.ToDate.DayNumber - request.FromDate.DayNumber + 1 > OperationsConstants.MaximumReconciliationRangeDays)
            throw new ValidationException(MessageCode.InvalidDateRange);
        await exclusions.ValidateAsync(request.FromDate, request.ToDate, ct);
        AccountReconciliationRunResponse run = await accounts.RunAsync(new AccountReconciliationRequest(request.FromDate, request.ToDate, null), ct);
        await RunCashChecksAsync(request.FromDate, request.ToDate, ct);
        return await ReadAsync(request.FromDate, request.ToDate, run.RunId, ct);
    }

    public async Task<UnifiedReconciliationResponse> ReadAsync(DateOnly from, DateOnly to, long runId, CancellationToken ct)
    {
        List<UnifiedExceptionRow> rows = await db.ReconciliationExceptions.AsNoTracking()
            .Where(x => x.BusinessDate >= from && x.BusinessDate <= to &&
                x.Status != OperationsConstants.ExceptionResolved && x.Status != OperationsConstants.ExceptionForcePassed)
            .OrderByDescending(x => x.BusinessDate).ThenByDescending(x => x.Id)
            .Select(x => new UnifiedExceptionRow(x.Id, x.Type, x.BusinessDate, x.AccountId,
                x.PositionSessionId, x.RelatedTransactionId, x.ExpectedAmount, x.ActualAmount,
                x.Difference, x.Status, x.AssignedTo, x.Notes)).ToListAsync(ct);
        return new(runId, from, to, rows.Where(x => x.PositionSessionId == null).ToList(),
            rows.Where(x => x.PositionSessionId != null).ToList());
    }

    public async Task<IReadOnlyList<UnifiedExceptionRow>> MyTasksAsync(CancellationToken ct)
    {
        long actor = currentUser.GetCurrentUserId();
        return await db.ReconciliationExceptions.AsNoTracking().Where(x => x.AssignedTo == actor &&
                x.Status != OperationsConstants.ExceptionResolved && x.Status != OperationsConstants.ExceptionForcePassed)
            .OrderByDescending(x => x.BusinessDate).Select(x => new UnifiedExceptionRow(x.Id, x.Type,
                x.BusinessDate, x.AccountId, x.PositionSessionId, x.RelatedTransactionId,
                x.ExpectedAmount, x.ActualAmount, x.Difference, x.Status, x.AssignedTo, x.Notes)).ToListAsync(ct);
    }

    /// <summary>Lists posted account and cash corrections needing checks, scoped to managers or the assigned investigator.</summary>
    public async Task<IReadOnlyList<ReconciliationVerificationTask>> VerificationTasksAsync(CancellationToken ct)
    {
        long actor = currentUser.GetCurrentUserId();
        bool canManage = await CanManageVerificationAsync(actor, ct);
        List<CorrectionProposal> accountProposals = await db.CorrectionProposals.AsNoTracking().Include(proposal => proposal.Exception)
            .Where(proposal => proposal.Status == ProposalStatus.Executed && proposal.Verification != VerificationStatus.Passed &&
                proposal.Exception!.Status != OperationsConstants.ExceptionForcePassed && (canManage || proposal.Exception.AssignedTo == actor))
            .ToListAsync(ct);
        List<CashCorrectionProposal> cashProposals = await db.CashCorrectionProposals.AsNoTracking().Include(proposal => proposal.Exception)
            .Where(proposal => proposal.Status == ProposalStatus.Executed && proposal.Verification != VerificationStatus.Passed &&
                proposal.Exception!.Status != OperationsConstants.ExceptionForcePassed && (canManage || proposal.Exception.AssignedTo == actor))
            .ToListAsync(ct);
        List<long> transactionIds = accountProposals.Select(proposal => proposal.ExecutionTransactionId)
            .Concat(cashProposals.Select(proposal => proposal.ExecutionTransactionId)).Where(id => id.HasValue)
            .Select(id => id!.Value).Distinct().ToList();
        Dictionary<long, DateOnly?> postingDates = await db.Transactions.AsNoTracking().Where(transaction => transactionIds.Contains(transaction.Id))
            .ToDictionaryAsync(transaction => transaction.Id, transaction => transaction.BusinessDate, ct);
        List<ReconciliationVerificationTask> tasks = [];
        foreach (CorrectionProposal proposal in accountProposals)
        {
            CorrectionPreview? preview = string.IsNullOrEmpty(proposal.PreviewJson)
                ? null : JsonSerializer.Deserialize<CorrectionPreview>(proposal.PreviewJson);
            DateOnly? postingDate = proposal.ExecutionTransactionId.HasValue
                ? postingDates.GetValueOrDefault(proposal.ExecutionTransactionId.Value) : preview?.PostingDate;
            tasks.Add(new(proposal.Id, proposal.ExceptionId, false, proposal.Kind.ToString(), proposal.Exception!.BusinessDate,
                postingDate, proposal.ExecutionTransactionId, proposal.Verification, proposal.VerificationNote, proposal.VerifiedAtUtc));
        }
        foreach (CashCorrectionProposal proposal in cashProposals)
        {
            DateOnly? postingDate = proposal.ExecutionTransactionId.HasValue
                ? postingDates.GetValueOrDefault(proposal.ExecutionTransactionId.Value) : null;
            tasks.Add(new(proposal.Id, proposal.ExceptionId, true, proposal.Kind.ToString(), proposal.Exception!.BusinessDate,
                postingDate, proposal.ExecutionTransactionId, proposal.Verification, proposal.VerificationNote, proposal.VerifiedAtUtc));
        }
        return tasks.OrderBy(task => task.SourceBusinessDate).ThenBy(task => task.ExceptionId).ThenBy(task => task.ProposalId).ToList();
    }

    /// <summary>Allows verification by the assigned investigator or a user with correction approval permission.</summary>
    public async Task RequireVerificationAccessAsync(ReconciliationException exception, CancellationToken ct)
    {
        if (exception.Status == OperationsConstants.ExceptionForcePassed)
            throw new ConflictException(MessageCode.Conflict);
        long actor = currentUser.GetCurrentUserId();
        if (exception.AssignedTo != actor && !await CanManageVerificationAsync(actor, ct))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
    }

    // Approval permission provides oversight of all verification tasks; other investigators see assigned work only.
    private Task<bool> CanManageVerificationAsync(long actor, CancellationToken ct) =>
        (from userRole in db.UserRoles
         join staff in db.Users on userRole.UserId equals staff.Id
         join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
         join permission in db.Permissions on rolePermission.PermissionId equals permission.Id
         where userRole.UserId == actor && staff.Status == UserStatus.Active && permission.Code == SecurityConstants.TransactionCorrectionApproval
         select userRole.UserId).AnyAsync(ct);

    public async Task AssignAsync(long exceptionId, AssignReconciliationRequest request, CancellationToken ct)
    {
        long actor = currentUser.GetCurrentUserId();
        bool eligible = await (from user in db.Users join ur in db.UserRoles on user.Id equals ur.UserId
            join role in db.Roles on ur.RoleId equals role.Id
            where user.Id == request.InvestigatorId && user.Status == UserStatus.Active &&
                (role.Code == SecurityConstants.OfficerRole || role.Code == SecurityConstants.AuditorRole)
            select user.Id).AnyAsync(ct);
        if (!eligible || request.Note?.Length > 2000) throw new ValidationException(MessageCode.InvalidRequest);
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        ReconciliationException exception = (await db.ReconciliationExceptions.FromSql($"SELECT * FROM ReconciliationExceptions WHERE Id = {exceptionId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (exception.Status is OperationsConstants.ExceptionResolved or OperationsConstants.ExceptionForcePassed)
            throw new ConflictException(MessageCode.Conflict);
        db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory { Exception = exception,
            OldStatus = exception.Status, NewStatus = OperationsConstants.ExceptionUnderInvestigation,
            Note = $"Assigned investigator {request.InvestigatorId}. {request.Note}", ActorId = actor, CreatedAtUtc = DateTime.UtcNow });
        long? previousAssignee = exception.AssignedTo;
        exception.AssignedTo = request.InvestigatorId;
        exception.Status = OperationsConstants.ExceptionUnderInvestigation;
        exception.Notes = request.Note;
        exception.UpdatedAtUtc = DateTime.UtcNow;
        await notifier.ExceptionAssignedAsync(exception, previousAssignee, actor, request.Note, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task FinalVaultCountAsync(FinalVaultCountRequest request, CancellationToken ct)
    {
        if (request.ActualAmount < 0m || decimal.Round(request.ActualAmount, 2) != request.ActualAmount ||
            request.Note?.Length > 1000) throw new ValidationException(MessageCode.InvalidRequest);
        long actor = currentUser.GetCurrentUserId();
        await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Status = 'Open' ORDER BY Date FOR UPDATE").ToListAsync(ct);
        if (!await db.BusinessDates.AnyAsync(x => x.Date == request.BusinessDate && x.Status == OperationsConstants.BusinessDateOpen, ct))
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        CashPositionSession session = (await db.CashPositionSessions.FromSql($"SELECT * FROM CashPositionSessions WHERE BusinessDate = {request.BusinessDate} AND PositionType = 'Vault' FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (session.Status != OperationsConstants.CashSessionOpen || session.ReservedCash != 0m ||
            await db.CashPositionSessions.AnyAsync(x => x.BusinessDate == request.BusinessDate && x.Id != session.Id && x.Status != OperationsConstants.CashSessionClosed, ct) ||
            await db.CashProposals.AnyAsync(x => x.Session!.BusinessDate == request.BusinessDate && (x.Status == "PendingApproval" || x.Status == "PendingReceipt"), ct) ||
            await db.CashCounts.AnyAsync(x => x.SessionId == session.Id, ct) ||
            await db.CashHandoffs.AnyAsync(x => x.BusinessDate == request.BusinessDate &&
                x.Status != OperationsConstants.CashHandoffAccepted, ct) ||
            await db.BankExpenseRequests.AnyAsync(x => x.BusinessDate == request.BusinessDate && x.Status == "PendingApproval", ct))
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        db.CashCounts.Add(new CashCount { Session = session, ExpectedAmount = session.ExpectedClosingCash,
            ActualAmount = request.ActualAmount, Difference = request.ActualAmount - session.ExpectedClosingCash,
            Notes = request.Note, CountedBy = actor, CountedAtUtc = DateTime.UtcNow });
        session.Status = OperationsConstants.CashSessionClosed;
        session.ClosedBy = actor;
        session.ClosedAtUtc = DateTime.UtcNow;
        session.Version++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await tx.DisposeAsync();
        await RunAsync(new UnifiedReconciliationRequest(request.BusinessDate, request.BusinessDate), ct);
    }

    private async Task RunCashChecksAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        List<CashPositionSession> sessions = await db.CashPositionSessions.Where(x => x.BusinessDate >= from && x.BusinessDate <= to).ToListAsync(ct);
        foreach (CashPositionSession session in sessions)
        {
            CashCount? count = await db.CashCounts.AsNoTracking().Where(x => x.SessionId == session.Id)
                .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            if (count is null) continue;
            decimal actual = await CountedCashAfterHandoffsAsync(session.Id, count, ct);
            decimal difference = actual - session.ExpectedClosingCash;
            ReconciliationException? exception = await db.ReconciliationExceptions.Where(x => x.Type == OperationsConstants.ExceptionCashBalance &&
                x.PositionSessionId == session.Id && x.Status != OperationsConstants.ExceptionResolved)
                .OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            if (difference == 0m && exception is not null && exception.Status != OperationsConstants.ExceptionForcePassed)
                SetStatus(exception, OperationsConstants.ExceptionResolved, "Physical cash count matches expected cash.");
            else if (difference != 0m && exception is null)
            {
                exception = new ReconciliationException { Type = OperationsConstants.ExceptionCashBalance,
                    Source = "UnifiedCashCheck", BusinessDate = session.BusinessDate, PositionSessionId = session.Id,
                    ExpectedAmount = session.ExpectedClosingCash, ActualAmount = actual,
                    Difference = difference, Severity = "Critical", Status = OperationsConstants.ExceptionOpen,
                    CreatedBy = currentUser.GetCurrentUserId(), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
                db.ReconciliationExceptions.Add(exception);
            }
            else if (exception is not null && exception.Status != OperationsConstants.ExceptionForcePassed)
            {
                exception.ExpectedAmount = session.ExpectedClosingCash;
                exception.ActualAmount = actual;
                exception.Difference = difference;
                exception.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        string cashCode = AccountingConstants.CashOnHandGlCode;
        List<Transaction> cashTransactions = await db.Transactions.AsNoTracking().Where(x => x.PostedAt.HasValue &&
            x.BusinessDate >= from && x.BusinessDate <= to &&
            db.TransactionEntries.Any(e => e.TransactionId == x.Id && e.GlAccount!.Code == cashCode) &&
            !db.CashMovements.Any(m => m.TransactionId == x.Id) &&
            x.TransactionType != TransactionType.ReconciliationAdjustment && x.TransactionType != TransactionType.ReconciliationRepair)
            .ToListAsync(ct);
        Dictionary<DateOnly, HashSet<long>> waivedByDate = [];
        foreach (DateOnly date in cashTransactions.Select(x => x.BusinessDate!.Value).Distinct())
            waivedByDate[date] = await exclusions.ActiveSourceIdsAsync(date, "Transaction", ct);
        foreach (Transaction item in cashTransactions)
        {
            if (waivedByDate[item.BusinessDate!.Value].Contains(item.Id)) continue;
            CashPositionSession? session = sessions.FirstOrDefault(x => x.BusinessDate == item.BusinessDate && x.TellerId == item.InitiatedBy);
            if (session is null) continue;
            bool exists = await db.ReconciliationExceptions.AnyAsync(x => x.Type == "CashMovementMissing" &&
                x.RelatedTransactionId == item.Id && x.Status != OperationsConstants.ExceptionResolved, ct);
            if (exists) continue;
            db.ReconciliationExceptions.Add(new ReconciliationException { Type = "CashMovementMissing",
                Source = "UnifiedCashCheck", BusinessDate = session.BusinessDate, PositionSessionId = session.Id,
                RelatedTransactionId = item.Id, ExpectedAmount = item.Amount, ActualAmount = 0,
                Difference = -item.Amount, Severity = "Critical", Status = OperationsConstants.ExceptionOpen,
                CreatedBy = currentUser.GetCurrentUserId(), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        }
        List<ReconciliationException> missingMovementExceptions = await db.ReconciliationExceptions
            .Where(x => x.Type == "CashMovementMissing" && x.BusinessDate >= from && x.BusinessDate <= to &&
                x.Status != OperationsConstants.ExceptionResolved && x.Status != OperationsConstants.ExceptionForcePassed &&
                x.RelatedTransactionId.HasValue).ToListAsync(ct);
        foreach (ReconciliationException exception in missingMovementExceptions)
        {
            if (!waivedByDate.TryGetValue(exception.BusinessDate, out HashSet<long>? waived))
            {
                waived = await exclusions.ActiveSourceIdsAsync(exception.BusinessDate, "Transaction", ct);
                waivedByDate[exception.BusinessDate] = waived;
            }
            if (waived.Contains(exception.RelatedTransactionId!.Value))
            {
                SetStatus(exception, OperationsConstants.ExceptionResolved, "Missing-movement validation excluded by an explicit source waiver; no financial posting or movement was created.");
                continue;
            }
            if (await db.CashMovements.AnyAsync(x => x.TransactionId == exception.RelatedTransactionId, ct))
                SetStatus(exception, OperationsConstants.ExceptionResolved, "Cash movement for the posted transaction is present.");
        }
        await db.SaveChangesAsync(ct);
    }

    private void SetStatus(ReconciliationException item, string status, string note)
    {
        db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory { Exception = item,
            OldStatus = item.Status, NewStatus = status, Note = note,
            ActorId = currentUser.GetCurrentUserId(), CreatedAtUtc = DateTime.UtcNow });
        item.Status = status;
        item.UpdatedAtUtc = DateTime.UtcNow;
    }

    // Custody corrections restate the original movement, rather than transferring physical cash again today.
    public async Task<decimal> CountedCashAfterHandoffsAsync(long sessionId, CashCount count, CancellationToken ct)
    {
        List<CashMovement> custody = await db.CashMovements.AsNoTracking().Where(x => x.SessionId == sessionId &&
            x.Status == OperationsConstants.CashMovementApproved && x.CreatedAtUtc > count.CountedAtUtc &&
            (x.Type == "VaultReturnOut" || x.Type == "VaultReturnIn" || x.Type == "TransferOut" || x.Type == "TransferIn" ||
             x.Type == "VaultIssue" || x.Type == "CashReceipt"))
            .ToListAsync(ct);
        List<CashMovement> corrections = await db.CashMovements.AsNoTracking().Where(x => x.SessionId == sessionId &&
            x.Status == OperationsConstants.CashMovementApproved && x.CorrectsMovementId.HasValue &&
            x.CorrectsMovement!.CreatedAtUtc > count.CountedAtUtc &&
            (x.CorrectsMovement.Type == "VaultReturnOut" || x.CorrectsMovement.Type == "VaultReturnIn" ||
             x.CorrectsMovement.Type == "TransferOut" || x.CorrectsMovement.Type == "TransferIn" ||
             x.CorrectsMovement.Type == "VaultIssue" || x.CorrectsMovement.Type == "CashReceipt"))
            .ToListAsync(ct);
        return count.ActualAmount + custody.Sum(x => x.Type is "VaultReturnIn" or "TransferIn" or "CashReceipt" ? x.Amount : -x.Amount)
            + corrections.Sum(x => x.Type == "CorrectionIn" ? x.Amount : -x.Amount);
    }
}
