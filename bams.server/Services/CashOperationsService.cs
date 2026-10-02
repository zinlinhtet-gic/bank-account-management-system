using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Owns teller/vault cash sessions, inter-position transfers, and physical counts.</summary>
public sealed class CashOperationsService(ApplicationDbContext db, ICurrentUserService currentUser,
    IBusinessDateService businessDates) : ICashOperationsService
{
    public async Task<CashPositionSessionResponse> OpenSessionAsync(OpenCashSessionRequest request, CancellationToken cancellationToken)
    {
        if (request.OpeningCash < 0m || decimal.Round(request.OpeningCash, 2) != request.OpeningCash)
            throw new ValidationException(MessageCode.InvalidAmount);
        if (request.PositionType is not ("Teller" or "Vault"))
            throw new ValidationException(MessageCode.InvalidRequest);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var actorId = currentUser.GetCurrentUserId();
        if (request.PositionType == OperationsConstants.CashPositionVault)
            await EnsureVaultPermissionAsync(actorId, cancellationToken);
        var date = await businessDates.GetPostingBusinessDateValueAsync(DateTime.UtcNow, cancellationToken);
        // Business-date row locking serializes session openings for this date, so this invariant
        // remains safe when two operators attempt to open a session concurrently.
        var sessionExistsForDate = await db.CashPositionSessions.AnyAsync(
            session => session.BusinessDate == date, cancellationToken);
        if (sessionExistsForDate)
            throw new ConflictException(MessageCode.CashSessionAlreadyExists);
        var now = DateTime.UtcNow;
        var session = new CashPositionSession
        {
            PositionType = request.PositionType,
            TellerId = request.PositionType == OperationsConstants.CashPositionTeller ? actorId : null,
            BusinessDate = date,
            OpeningCash = request.OpeningCash, ExpectedClosingCash = request.OpeningCash, Status = "Open",
            OpenedAtUtc = now, OpenedBy = actorId
        };
        db.CashPositionSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(session);
    }

    public async Task<IReadOnlyList<CashPositionSessionResponse>> GetSessionsAsync(DateOnly? businessDate, CancellationToken cancellationToken)
    {
        var date = businessDate ?? await GetOpenBusinessDateAsync(cancellationToken);
        var actorId = currentUser.GetCurrentUserId();
        var query = db.CashPositionSessions.AsNoTracking().Where(session => session.BusinessDate == date);
        var canManageCash = await HasVaultPermissionAsync(actorId, cancellationToken);
        var sessions = await query.Where(session =>
                (canManageCash && session.PositionType == OperationsConstants.CashPositionVault) ||
                (canManageCash && session.PositionType == OperationsConstants.CashPositionTeller) ||
                (session.PositionType == OperationsConstants.CashPositionTeller && session.TellerId == actorId))
            .OrderBy(session => session.Id).ToListAsync(cancellationToken);
        return sessions.Select(ToResponse).ToList();
    }

    public async Task<CashPositionSessionDetailResponse> GetSessionDetailAsync(long sessionId, CancellationToken cancellationToken)
    {
        var session = await db.CashPositionSessions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        var actorId = currentUser.GetCurrentUserId();
        var canManageCash = await HasVaultPermissionAsync(actorId, cancellationToken);
        if ((session.PositionType == OperationsConstants.CashPositionVault || session.TellerId != actorId) && !canManageCash)
            throw new ForbiddenException(MessageCode.InsufficientPermission);

        var movements = await db.CashMovements.AsNoTracking().Where(item => item.SessionId == sessionId)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new CashMovementHistoryResponse(item.Id, item.Type, item.Status, item.Amount,
                item.DestinationSessionId, item.TransactionId, item.CorrectionTransactionId, item.ActorId,
                item.ApprovedBy, item.CreatedAtUtc, item.Note)).ToListAsync(cancellationToken);
        var counts = await db.CashCounts.AsNoTracking().Where(item => item.SessionId == sessionId)
            .OrderBy(item => item.CountedAtUtc)
            .Select(item => new CashCountHistoryResponse(item.Id, item.ExpectedAmount, item.ActualAmount,
                item.Difference, item.CountedBy, item.CountedAtUtc, item.Notes)).ToListAsync(cancellationToken);
        return new CashPositionSessionDetailResponse(ToResponse(session), movements, counts);
    }

    public async Task<CashPositionSessionResponse> TransferCashAsync(long sessionId, TransferCashRequest request, CancellationToken cancellationToken)
    {
        TransactionRequestValidator.ValidateAmount(request.Amount);
        TransactionRequestValidator.EnsureMaximumLength(request.Note, 500);
        var actorId = currentUser.GetCurrentUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var sessionIds = new[] { sessionId, request.DestinationSessionId }.Distinct().Order().ToArray();
        var lockedSessions = new Dictionary<long, CashPositionSession>();
        foreach (var id in sessionIds) lockedSessions.Add(id, await LockSessionAsync(id, cancellationToken));
        var source = lockedSessions[sessionId];
        var destination = lockedSessions[request.DestinationSessionId];
        EnsureOpenForActor(source, actorId);
        if (source.PositionType == OperationsConstants.CashPositionVault || destination.PositionType == OperationsConstants.CashPositionVault)
            await EnsureVaultPermissionAsync(actorId, cancellationToken);
        if (destination.Status != "Open" || source.Id == destination.Id || source.BusinessDate != destination.BusinessDate)
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        if (source.ExpectedClosingCash < request.Amount)
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
        var now = DateTime.UtcNow;
        source.ExpectedClosingCash -= request.Amount;
        destination.ExpectedClosingCash += request.Amount;
        AddMovement(source, destination, "TransferOut", request.Amount, actorId, now, request.Note);
        AddMovement(destination, source, "TransferIn", request.Amount, actorId, now, request.Note);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(source);
    }

    public async Task<CashCountResponse> SubmitCountAsync(long sessionId, SubmitCashCountRequest request, CancellationToken cancellationToken)
    {
        if (request.ActualAmount < 0m || decimal.Round(request.ActualAmount, 2) != request.ActualAmount)
            throw new ValidationException(MessageCode.InvalidAmount);
        TransactionRequestValidator.EnsureMaximumLength(request.Notes, 2000);
        var actorId = currentUser.GetCurrentUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var session = await LockSessionAsync(sessionId, cancellationToken);
        EnsureAssignedToActor(session, actorId);
        if (session.PositionType == OperationsConstants.CashPositionVault)
            await EnsureVaultPermissionAsync(actorId, cancellationToken);
        var now = DateTime.UtcNow;
        var difference = request.ActualAmount - session.ExpectedClosingCash;
        if (session.Status == OperationsConstants.CashSessionOpen)
        {
            session.Status = OperationsConstants.CashSessionClosed;
            session.ClosedBy = actorId;
            session.ClosedAtUtc = now;
        }
        var count = new CashCount
        {
            SessionId = session.Id, ExpectedAmount = session.ExpectedClosingCash, ActualAmount = request.ActualAmount,
            Difference = difference, Notes = request.Notes, CountedBy = actorId, CountedAtUtc = now
        };
        db.CashCounts.Add(count);
        var existingException = await db.ReconciliationExceptions.Where(item => item.Type == OperationsConstants.ExceptionCashBalance &&
                item.PositionSessionId == session.Id && item.Status != OperationsConstants.ExceptionResolved)
            .OrderByDescending(item => item.Id).FirstOrDefaultAsync(cancellationToken);
        if (difference == 0m && existingException is not null)
        {
            db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
            {
                Exception = existingException, OldStatus = existingException.Status, NewStatus = OperationsConstants.ExceptionResolved,
                Note = "A subsequent physical count matched expected cash.", ActorId = actorId, CreatedAtUtc = now
            });
            existingException.Status = OperationsConstants.ExceptionResolved;
            existingException.UpdatedAtUtc = now;
            existingException.ActualAmount = request.ActualAmount;
            existingException.Difference = 0m;
        }
        else if (difference != 0m && existingException is null)
        {
            var exception = new ReconciliationException
            {
                Type = "CashBalance", Source = "PhysicalCount", BusinessDate = session.BusinessDate,
                PositionSession = session, ExpectedAmount = session.ExpectedClosingCash, ActualAmount = request.ActualAmount,
                Difference = difference, Severity = "Critical", Status = "Open", CreatedBy = actorId,
                CreatedAtUtc = now, UpdatedAtUtc = now, Notes = request.Notes
            };
            db.ReconciliationExceptions.Add(exception);
            db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
            {
                Exception = exception,
                OldStatus = "None",
                NewStatus = exception.Status,
                Note = request.Notes ?? "Physical cash count did not match expected closing cash.",
                ActorId = actorId,
                CreatedAtUtc = now
            });
        }
        else if (existingException is not null)
        {
            db.ReconciliationExceptionHistories.Add(new ReconciliationExceptionHistory
            {
                Exception = existingException,
                OldStatus = existingException.Status,
                NewStatus = existingException.Status,
                Note = $"Physical recount recorded. Expected {session.ExpectedClosingCash:N2}; actual {request.ActualAmount:N2}; difference {difference:N2}.",
                ActorId = actorId,
                CreatedAtUtc = now
            });
            existingException.ExpectedAmount = session.ExpectedClosingCash;
            existingException.ActualAmount = request.ActualAmount;
            existingException.Difference = difference;
            existingException.UpdatedAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CashCountResponse(count.Id, session.Id, count.ExpectedAmount, count.ActualAmount, difference,
            difference == 0m ? "Matched" : "Unmatched", now);
    }

    /// <summary>Requests an expected-cash adjustment backed by a posted cash-on-hand correction transaction.</summary>
    public async Task<CashAdjustmentResponse> RequestAdjustmentAsync(long sessionId, RequestCashAdjustmentRequest request, CancellationToken cancellationToken)
    {
        if (request.SignedAmount == 0m || decimal.Round(request.SignedAmount, 2) != request.SignedAmount || request.CorrectionTransactionId <= 0)
            throw new ValidationException(MessageCode.InvalidAmount);
        TransactionRequestValidator.EnsureMaximumLength(request.Note, 500);
        var actorId = currentUser.GetCurrentUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var session = await LockSessionAsync(sessionId, cancellationToken);
        EnsureAdjustmentEligible(session, actorId);
        if (session.PositionType == OperationsConstants.CashPositionVault)
            await EnsureVaultPermissionAsync(actorId, cancellationToken);
        var latestCount = await db.CashCounts.AsNoTracking().Where(item => item.SessionId == session.Id)
            .OrderByDescending(item => item.CountedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (latestCount is null || latestCount.Difference == 0m || latestCount.Difference != request.SignedAmount)
            throw new BusinessRuleException(MessageCode.CashAdjustmentApprovalRequired);

        var correction = await db.Transactions.AsNoTracking().SingleOrDefaultAsync(item =>
            item.Id == request.CorrectionTransactionId && item.PostedAt.HasValue &&
            (item.TransactionStatus == TransactionStatus.Posted || item.TransactionStatus == TransactionStatus.Completed ||
             item.TransactionStatus == TransactionStatus.Accrued || item.TransactionStatus == TransactionStatus.Reversed),
            cancellationToken) ?? throw new NotFoundException(MessageCode.TransactionNotFound);
        var hasExistingMovement = await db.CashMovements.AnyAsync(item =>
            item.TransactionId == correction.Id || item.CorrectionTransactionId == correction.Id, cancellationToken);
        if (hasExistingMovement)
            throw new ConflictException(MessageCode.Conflict);

        var cashEffect = await db.TransactionEntries.AsNoTracking()
            .Where(entry => entry.TransactionId == correction.Id && entry.PostingDate == session.BusinessDate &&
                entry.GlAccount!.Code == AccountingConstants.CashOnHandGlCode)
            .SumAsync(entry => entry.EntryType == EntryType.Debit ? entry.Amount : -entry.Amount, cancellationToken);
        if (cashEffect == 0m || cashEffect != request.SignedAmount)
            throw new BusinessRuleException(MessageCode.CashAdjustmentApprovalRequired);

        var movement = new CashMovement
        {
            SessionId = session.Id,
            Type = OperationsConstants.CashMovementAdjustment,
            Status = OperationsConstants.CashMovementPendingApproval,
            Amount = Math.Abs(request.SignedAmount),
            CorrectionTransactionId = correction.Id,
            ActorId = actorId,
            CreatedAtUtc = DateTime.UtcNow,
            Note = request.Note
        };
        db.CashMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToAdjustmentResponse(movement, request.SignedAmount);
    }

    /// <summary>Approves a distinct user's pending cash correction and applies its signed effect to the position.</summary>
    public async Task<CashAdjustmentResponse> ApproveAdjustmentAsync(long movementId, CancellationToken cancellationToken)
    {
        var actorId = currentUser.GetCurrentUserId();
        if (!await HasVaultPermissionAsync(actorId, cancellationToken))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var movement = (await db.CashMovements.FromSql($"SELECT * FROM CashMovements WHERE Id = {movementId} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault() ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (movement.Type != OperationsConstants.CashMovementAdjustment || movement.Status != OperationsConstants.CashMovementPendingApproval || movement.ActorId == actorId)
            throw new BusinessRuleException(MessageCode.CashAdjustmentApprovalRequired);
        var session = await LockSessionAsync(movement.SessionId, cancellationToken);
        if (session.Status is not (OperationsConstants.CashSessionOpen or OperationsConstants.CashSessionClosed))
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        var correctionId = movement.CorrectionTransactionId ?? throw new BusinessRuleException(MessageCode.CashAdjustmentApprovalRequired);
        var latestCount = await db.CashCounts.AsNoTracking().Where(item => item.SessionId == session.Id)
            .OrderByDescending(item => item.CountedAtUtc).FirstOrDefaultAsync(cancellationToken);
        var signedAmount = await GetCashEffectAsync(correctionId, session.BusinessDate, cancellationToken);
        if (latestCount is null || latestCount.Difference != signedAmount)
            throw new BusinessRuleException(MessageCode.CashAdjustmentApprovalRequired);
        if (signedAmount == 0m || signedAmount != (signedAmount > 0m ? movement.Amount : -movement.Amount))
            throw new BusinessRuleException(MessageCode.CashAdjustmentApprovalRequired);
        // A posted correction must not reduce the operational cash position below zero.
        if (session.ExpectedClosingCash + signedAmount < 0m)
            throw new BusinessRuleException(MessageCode.InsufficientCashPositionBalance);
        movement.Status = OperationsConstants.CashMovementApproved;
        movement.ApprovedBy = actorId;
        movement.ApprovedAtUtc = DateTime.UtcNow;
        session.ExpectedClosingCash += signedAmount;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToAdjustmentResponse(movement, signedAmount);
    }

    /// <summary>Lists posted adjustment requests for approval review.</summary>
    public async Task<IReadOnlyList<CashAdjustmentResponse>> GetAdjustmentsAsync(DateOnly? businessDate, string? status, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(status) && status is not (OperationsConstants.CashMovementPendingApproval or OperationsConstants.CashMovementApproved))
            throw new ValidationException(MessageCode.InvalidRequest);
        var query = from movement in db.CashMovements.AsNoTracking()
                    join session in db.CashPositionSessions.AsNoTracking() on movement.SessionId equals session.Id
                    where movement.Type == OperationsConstants.CashMovementAdjustment
                    select new { movement, session };
        if (businessDate.HasValue) query = query.Where(item => item.session.BusinessDate == businessDate.Value);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(item => item.movement.Status == status);
        var items = await query.OrderByDescending(item => item.movement.CreatedAtUtc)
            .Select(item => new CashAdjustmentResponse(item.movement.Id, item.movement.SessionId,
                item.movement.CorrectionTransactionId.HasValue
                    ? db.TransactionEntries.Where(entry => entry.TransactionId == item.movement.CorrectionTransactionId.Value &&
                        entry.PostingDate == item.session.BusinessDate && entry.GlAccount!.Code == AccountingConstants.CashOnHandGlCode)
                        .Sum(entry => entry.EntryType == EntryType.Debit ? entry.Amount : -entry.Amount)
                    : 0m,
                item.movement.CorrectionTransactionId ?? 0L, item.movement.Status, item.movement.ActorId,
                item.movement.CreatedAtUtc, item.movement.ApprovedBy, item.movement.ApprovedAtUtc, item.movement.Note))
            .ToListAsync(cancellationToken);
        return items;
    }

    /// <summary>Records a transaction's cash effect against the actor's open teller session for the posting date.</summary>
    public async Task AddTransactionMovementAsync(bams.server.Models.Transactions.Transaction transaction, bool isDeposit, long actorId, decimal amount, CancellationToken cancellationToken)
    {
        var postingDate = await businessDates.GetPostingBusinessDateValueAsync(DateTime.UtcNow, cancellationToken);
        var currentSessions = await db.CashPositionSessions
            .FromSql($"SELECT * FROM CashPositionSessions WHERE TellerId = {actorId} AND PositionType = {OperationsConstants.CashPositionTeller} AND BusinessDate = {postingDate} AND Status = {OperationsConstants.CashSessionOpen} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var session = currentSessions.SingleOrDefault()
            ?? throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        // Reject a withdrawal before recording its movement if it would make expected physical cash negative.
        if (!isDeposit && session.ExpectedClosingCash < amount)
            throw new BusinessRuleException(MessageCode.InsufficientCashPositionBalance);
        session.ExpectedClosingCash += isDeposit ? amount : -amount;
        db.CashMovements.Add(new CashMovement
        {
            SessionId = session.Id, Type = isDeposit ? "Deposit" : "Withdrawal", Amount = amount,
            Transaction = transaction, ActorId = actorId, CreatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task<DateOnly> GetOpenBusinessDateAsync(CancellationToken cancellationToken)
    {
        var open = await db.BusinessDates.AsNoTracking().Where(item => item.Status == "Open")
            .OrderByDescending(item => item.Date).Select(item => (DateOnly?)item.Date).FirstOrDefaultAsync(cancellationToken);
        return open ?? BusinessTime.Today;
    }

    private async Task<CashPositionSession> LockSessionAsync(long id, CancellationToken cancellationToken) =>
        (await db.CashPositionSessions.FromSql($"SELECT * FROM CashPositionSessions WHERE Id = {id} FOR UPDATE").ToListAsync(cancellationToken))
            .SingleOrDefault() ?? throw new NotFoundException(MessageCode.ResourceNotFound);

    // Vault custody is limited to managers with the separately seeded dual-control permission.
    private async Task EnsureVaultPermissionAsync(long actorId, CancellationToken cancellationToken)
    {
        if (!await HasVaultPermissionAsync(actorId, cancellationToken))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
    }

    private async Task<bool> HasVaultPermissionAsync(long actorId, CancellationToken cancellationToken) =>
        await (from userRole in db.UserRoles
            join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
            join permission in db.Permissions on rolePermission.PermissionId equals permission.Id
            where userRole.UserId == actorId && permission.Code == SecurityConstants.EndOfDayApproval
            select permission.Id).AnyAsync(cancellationToken);

    private async Task<decimal> GetCashEffectAsync(long transactionId, DateOnly businessDate, CancellationToken cancellationToken) =>
        await db.TransactionEntries.AsNoTracking()
            .Where(entry => entry.TransactionId == transactionId && entry.PostingDate == businessDate &&
                entry.GlAccount!.Code == AccountingConstants.CashOnHandGlCode)
            .SumAsync(entry => entry.EntryType == EntryType.Debit ? entry.Amount : -entry.Amount, cancellationToken);

    private static CashAdjustmentResponse ToAdjustmentResponse(CashMovement movement, decimal signedAmount) =>
        new(movement.Id, movement.SessionId, signedAmount, movement.CorrectionTransactionId ?? 0L,
            movement.Status, movement.ActorId, movement.CreatedAtUtc, movement.ApprovedBy, movement.ApprovedAtUtc, movement.Note);

    private static void EnsureOpenForActor(CashPositionSession session, long actorId)
    {
        if (session.Status != "Open" || (session.PositionType == "Teller" && session.TellerId != actorId))
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
    }

    private static void EnsureAssignedToActor(CashPositionSession session, long actorId)
    {
        if (session.PositionType == OperationsConstants.CashPositionTeller && session.TellerId != actorId)
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        if (session.Status is not (OperationsConstants.CashSessionOpen or OperationsConstants.CashSessionClosed))
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
    }

    // A counted/closed position remains eligible for an authorized correction followed by a recount.
    private static void EnsureAdjustmentEligible(CashPositionSession session, long actorId)
    {
        if (session.Status is not (OperationsConstants.CashSessionOpen or OperationsConstants.CashSessionClosed) ||
            (session.PositionType == OperationsConstants.CashPositionTeller && session.TellerId != actorId))
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
    }

    private void AddMovement(CashPositionSession session, CashPositionSession destination, string type, decimal amount, long actorId, DateTime now, string? note) =>
        db.CashMovements.Add(new CashMovement
        {
            SessionId = session.Id, DestinationSessionId = destination.Id, Type = type, Amount = amount,
            ActorId = actorId, CreatedAtUtc = now, Note = note
        });

    private static CashPositionSessionResponse ToResponse(CashPositionSession session) => new(
        session.Id, session.PositionType, session.TellerId, session.BusinessDate,
        session.OpeningCash, session.ExpectedClosingCash, session.Status);
}
