using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Transactions;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Owns teller/vault cash sessions, inter-position transfers, and physical counts.</summary>
public sealed partial class CashOperationsService(ApplicationDbContext db, ICurrentUserService currentUser,
    IBusinessDateService businessDates, CashSessionSchedulingService scheduling) : ICashOperationsService
{
    public async Task<CashPositionSessionResponse> OpenSessionAsync(OpenCashSessionRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var key = NormalizeCashIdempotencyKey(idempotencyKey);
        if (request.OpeningCash < 0m || decimal.Round(request.OpeningCash, 2) != request.OpeningCash ||
            request.PositionType == OperationsConstants.CashPositionTeller && request.OpeningCash <= 0m)
            throw new ValidationException(MessageCode.InvalidAmount);
        if (request.PositionType is not ("Teller" or "Vault"))
            throw new ValidationException(MessageCode.InvalidRequest);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var actorId = currentUser.GetCurrentUserId();
        var date = BusinessTime.Today;
        var lockedBusinessDate = (await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Date = {date} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        if (lockedBusinessDate is null || lockedBusinessDate.Status != OperationsConstants.BusinessDateOpen)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        await EnsureVaultPermissionAsync(actorId, cancellationToken);
        if (request.PositionType == OperationsConstants.CashPositionTeller)
        {
            if (request.TellerId is null || request.ScheduledStartAtMyanmar is null || request.ScheduledEndAtMyanmar is null ||
                request.ScheduledStartAtMyanmar.Value.Kind != DateTimeKind.Unspecified || request.ScheduledEndAtMyanmar.Value.Kind != DateTimeKind.Unspecified ||
                DateOnly.FromDateTime(request.ScheduledStartAtMyanmar.Value) != date ||
                DateOnly.FromDateTime(request.ScheduledEndAtMyanmar.Value) != date ||
                request.ScheduledEndAtMyanmar.Value <= request.ScheduledStartAtMyanmar.Value)
                throw new ValidationException(MessageCode.RequiredFieldMissing);
            bool assignedOfficer = await (from userRole in db.UserRoles
                join role in db.Roles on userRole.RoleId equals role.Id
                join user in db.Users on userRole.UserId equals user.Id
                where user.Id == request.TellerId.Value && user.Status == UserStatus.Active && role.Code == SecurityConstants.OfficerRole
                select user.Id).AnyAsync(cancellationToken);
            if (!assignedOfficer) throw new ValidationException(MessageCode.InvalidRequest);
        }
        else if (request.TellerId.HasValue || request.ScheduledStartAtMyanmar.HasValue || request.ScheduledEndAtMyanmar.HasValue)
            throw new ValidationException(MessageCode.InvalidRequest);
        var priorOpen = await db.CashPositionSessions.AsNoTracking().FirstOrDefaultAsync(
            item => item.OpenedBy == actorId && item.OpenIdempotencyKey == key, cancellationToken);
        if (priorOpen is not null)
        {
            if (!await MatchesOpenRequestAsync(priorOpen, request, cancellationToken))
                throw new ConflictException(MessageCode.IdempotencyKeyReused);
            return ToResponse(priorOpen);
        }

        date = await businessDates.GetPostingBusinessDateValueAsync(DateTime.UtcNow, cancellationToken);
        priorOpen = (await db.CashPositionSessions.FromSql(
            $"SELECT * FROM CashPositionSessions WHERE OpenedBy = {actorId} AND OpenIdempotencyKey = {key} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        if (priorOpen is not null)
        {
            if (!await MatchesOpenRequestAsync(priorOpen, request, cancellationToken))
                throw new ConflictException(MessageCode.IdempotencyKeyReused);
            return ToResponse(priorOpen);
        }
        var tellerId = request.PositionType == OperationsConstants.CashPositionTeller ? request.TellerId : null;
        var sessionExistsForDate = request.PositionType == OperationsConstants.CashPositionVault && await db.CashPositionSessions.AnyAsync(
            session => session.BusinessDate == date && session.PositionType == request.PositionType &&
                session.Status != OperationsConstants.CashSessionClosed, cancellationToken);
        if (sessionExistsForDate)
            throw new ConflictException(MessageCode.CashSessionAlreadyExists);

        CashPositionSession? vault = null;
        if (request.PositionType == OperationsConstants.CashPositionTeller)
        {
            vault = (await db.CashPositionSessions.FromSql(
                $"SELECT * FROM CashPositionSessions WHERE BusinessDate = {date} AND PositionType = {OperationsConstants.CashPositionVault} AND Status = {OperationsConstants.CashSessionOpen} FOR UPDATE")
                .ToListAsync(cancellationToken)).SingleOrDefault()
                ?? throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
            if (vault.ExpectedClosingCash - vault.ReservedCash < request.OpeningCash)
                throw new BusinessRuleException(MessageCode.InsufficientBalance);
        }

        if (tellerId.HasValue && await db.CashPositionSessions.AnyAsync(item => item.TellerId == tellerId && item.BusinessDate == date &&
            item.Status != OperationsConstants.CashSessionCancelled && item.ScheduledStartAtMyanmar < request.ScheduledEndAtMyanmar &&
            item.ScheduledEndAtMyanmar > request.ScheduledStartAtMyanmar, cancellationToken))
            throw new ConflictException(MessageCode.CashSessionAlreadyExists);
        var now = DateTime.UtcNow;
        var session = new CashPositionSession
        {
            PositionType = request.PositionType,
            TellerId = request.PositionType == OperationsConstants.CashPositionTeller ? request.TellerId : null,
            BusinessDate = date,
            OpeningCash = request.PositionType == OperationsConstants.CashPositionVault ? request.OpeningCash : 0m,
            ExpectedClosingCash = request.PositionType == OperationsConstants.CashPositionVault ? request.OpeningCash : 0m,
            Status = request.PositionType == OperationsConstants.CashPositionTeller ? OperationsConstants.CashSessionScheduled : OperationsConstants.CashSessionOpen,
            ScheduledStartAtMyanmar = request.ScheduledStartAtMyanmar,
            ScheduledEndAtMyanmar = request.ScheduledEndAtMyanmar,
            OpenedAtUtc = now, OpenedBy = actorId, OpenIdempotencyKey = key
        };
        db.CashPositionSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        if (vault is not null)
        {
            vault.ReservedCash += request.OpeningCash;
            vault.Version++;
            db.CashProposals.Add(new CashProposal
            {
                SessionId = session.Id,
                VaultSessionId = vault.Id,
                OfficerId = tellerId!.Value,
                RequestedAmount = request.OpeningCash,
                ApprovedAmount = request.OpeningCash,
                IsInitialCash = true,
                Status = "PendingReceipt",
                Note = "Manager-approved starting cash",
                CreatedAtUtc = now,
                ApprovedAtUtc = now,
                ApprovedBy = actorId
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(session);
    }

    public async Task<IReadOnlyList<CashPositionSessionResponse>> GetSessionsAsync(DateOnly? businessDate, CancellationToken cancellationToken)
    {
        var date = businessDate ?? BusinessTime.Today;
        var actorId = currentUser.GetCurrentUserId();
        var query = db.CashPositionSessions.AsNoTracking().Include(session => session.Teller).Where(session => session.BusinessDate == date);
        var canManageCash = await HasVaultPermissionAsync(actorId, cancellationToken);
        var canReviewAllCash = canManageCash || await HasAuditPermissionAsync(actorId, cancellationToken);
        var sessions = await query.Where(session =>
                (canReviewAllCash && session.PositionType == OperationsConstants.CashPositionVault) ||
                (canReviewAllCash && session.PositionType == OperationsConstants.CashPositionTeller) ||
                (session.PositionType == OperationsConstants.CashPositionTeller && session.TellerId == actorId))
            .OrderBy(session => session.Id).ToListAsync(cancellationToken);
        var day = await db.BusinessDates.AsNoTracking().SingleOrDefaultAsync(item => item.Date == date, cancellationToken);
        var ids = sessions.Select(item => item.Id).ToList();
        var startingCash = await db.CashProposals.AsNoTracking().Where(item => ids.Contains(item.SessionId) && item.IsInitialCash)
            .ToDictionaryAsync(item => item.SessionId, item => item.ApprovedAmount ?? 0m, cancellationToken);
        bool hasOpen = sessions.Any(item => item.TellerId == actorId && item.Status == OperationsConstants.CashSessionOpen);
        return sessions.Select(item => WithCapabilities(ToResponse(item) with { ScheduledStartingCash = startingCash.GetValueOrDefault(item.Id) },
            actorId, canManageCash, hasOpen, day)).ToList();
    }

    public async Task<CashPositionSessionResponse> ActivateSessionAsync(long sessionId, ActivateCashSessionRequest request, CancellationToken cancellationToken)
    {
        long actorId = currentUser.GetCurrentUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Users.FromSql($"SELECT * FROM Users WHERE Id = {actorId} FOR UPDATE").ToListAsync(cancellationToken);
        CashPositionSession session = await LockSessionAsync(sessionId, cancellationToken);
        if (session.TellerId == actorId && session.Status == OperationsConstants.CashSessionOpen && session.ActivatedAtUtc.HasValue &&
            await db.CashProposals.AnyAsync(item => item.SessionId == session.Id && item.IsInitialCash && item.Status == "Received", cancellationToken))
            return ToResponse(session);
        if (session.PositionType != OperationsConstants.CashPositionTeller || session.TellerId != actorId ||
            session.Status != OperationsConstants.CashSessionScheduled)
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        if (session.Version != request.ExpectedSessionVersion) throw new ConflictException(MessageCode.ConcurrentModification);
        DateTime nowUtc = DateTime.UtcNow;
        DateTime nowMyanmar = BusinessTime.ToBusinessDateTime(nowUtc);
        if (session.BusinessDate != BusinessTime.Today || session.ScheduledStartAtMyanmar > nowMyanmar ||
            session.ScheduledEndAtMyanmar <= nowMyanmar ||
            !await db.BusinessDates.AnyAsync(day => day.Date == session.BusinessDate && day.IsActive && day.Status == OperationsConstants.BusinessDateOpen, cancellationToken))
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        if (await db.CashPositionSessions.AnyAsync(item => item.TellerId == actorId && item.BusinessDate == session.BusinessDate &&
                item.Status == OperationsConstants.CashSessionOpen && item.PositionType == OperationsConstants.CashPositionTeller,
                cancellationToken))
            throw new ConflictException(MessageCode.CashSessionAlreadyExists);
        session.Status = OperationsConstants.CashSessionOpen;
        session.ActivatedAtUtc = nowUtc;
        // Activation acknowledges the already approved starting cash in the same transaction.
        var initial = await db.CashProposals.SingleOrDefaultAsync(item => item.SessionId == session.Id && item.IsInitialCash && item.Status == "PendingReceipt", cancellationToken)
            ?? throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        var vault = await LockSessionAsync(initial.VaultSessionId, cancellationToken);
        var amount = initial.ApprovedAmount!.Value;
        if (vault.Status != OperationsConstants.CashSessionOpen || vault.ReservedCash < amount || vault.ExpectedClosingCash < amount)
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
        vault.ReservedCash -= amount;
        vault.ExpectedClosingCash -= amount;
        vault.Version++;
        session.OpeningCash = amount;
        session.ExpectedClosingCash += amount;
        initial.Status = "Received";
        initial.ResolvedAtUtc = nowUtc;
        initial.Version++;
        AddMovement(vault, session, "VaultIssue", amount, actorId, nowUtc, "Starting cash accepted");
        AddMovement(session, vault, "CashReceipt", amount, actorId, nowUtc, "Starting cash accepted");
        session.Version++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(session);
    }

    public async Task<CashPositionSessionResponse> CancelScheduledSessionAsync(long sessionId, CancellationToken cancellationToken)
    {
        long actorId = currentUser.GetCurrentUserId();
        await EnsureVaultPermissionAsync(actorId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        CashPositionSession session = await LockSessionAsync(sessionId, cancellationToken);
        if (session.Status == OperationsConstants.CashSessionCancelled) return ToResponse(session);
        if (session.PositionType != OperationsConstants.CashPositionTeller || session.Status != OperationsConstants.CashSessionScheduled)
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        await ReleaseUnacceptedInitialCashAsync(session, DateTime.UtcNow, cancellationToken);
        session.Status = OperationsConstants.CashSessionCancelled;
        session.ClosedBy = actorId;
        session.ClosedAtUtc = DateTime.UtcNow;
        session.Version++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(session);
    }

    public async Task<IReadOnlyList<CashSessionAssigneeResponse>> GetSessionAssigneesAsync(CancellationToken cancellationToken)
    {
        long actorId = currentUser.GetCurrentUserId();
        await EnsureVaultPermissionAsync(actorId, cancellationToken);
        return await (from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where user.Status == UserStatus.Active && role.Code == SecurityConstants.OfficerRole
            orderby user.FullName, user.Username
            select new CashSessionAssigneeResponse(user.Id, user.FullName, user.Username)).Distinct().ToListAsync(cancellationToken);
    }

    public async Task<CashProposalResponse> CreateCashProposalAsync(long sessionId, CreateCashProposalRequest request, CancellationToken cancellationToken)
    {
        long actorId = currentUser.GetCurrentUserId();
        if (request.RequestedAmount is <= 0m || request.RequestedAmount.HasValue && decimal.Round(request.RequestedAmount.Value, 2) != request.RequestedAmount.Value)
            throw new ValidationException(MessageCode.InvalidAmount);
        string? note = TransactionRequestValidator.TrimToNull(request.Note);
        if (note is null) throw new ValidationException(MessageCode.RequiredFieldMissing);
        TransactionRequestValidator.EnsureMaximumLength(note, 500);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        CashPositionSession session = await LockSessionAsync(sessionId, cancellationToken);
        if (session.PositionType != OperationsConstants.CashPositionTeller || session.TellerId != actorId || session.Status != OperationsConstants.CashSessionOpen)
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        if (session.BusinessDate != BusinessTime.Today || !await db.BusinessDates.AnyAsync(day => day.Date == session.BusinessDate && day.IsActive && day.Status == OperationsConstants.BusinessDateOpen, cancellationToken))
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        CashPositionSession vault = await db.CashPositionSessions.SingleOrDefaultAsync(item => item.BusinessDate == session.BusinessDate &&
            item.PositionType == OperationsConstants.CashPositionVault && item.Status == OperationsConstants.CashSessionOpen,
            cancellationToken) ?? throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        CashProposal proposal = new()
        {
            SessionId = session.Id, VaultSessionId = vault.Id, OfficerId = actorId,
            RequestedAmount = request.RequestedAmount, Note = note, Status = "PendingApproval", CreatedAtUtc = DateTime.UtcNow
        };
        db.CashProposals.Add(proposal);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToProposalResponse(proposal);
    }

    public async Task<IReadOnlyList<CashProposalResponse>> GetCashProposalsAsync(DateOnly? businessDate, CancellationToken cancellationToken)
    {
        long actorId = currentUser.GetCurrentUserId();
        bool canReviewAll = await HasVaultPermissionAsync(actorId, cancellationToken);
        IQueryable<CashProposal> query = db.CashProposals.AsNoTracking().Include(item => item.Session);
        if (businessDate.HasValue) query = query.Where(item => item.Session!.BusinessDate == businessDate.Value);
        if (!canReviewAll) query = query.Where(item => item.OfficerId == actorId);
        var proposals = await query.OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
        return proposals.Select(ToProposalResponse).ToList();
    }

    public async Task<CashProposalResponse> ApproveCashProposalAsync(long proposalId, ApproveCashProposalRequest request, CancellationToken cancellationToken)
    {
        if (request.ApprovedAmount <= 0m || decimal.Round(request.ApprovedAmount, 2) != request.ApprovedAmount)
            throw new ValidationException(MessageCode.InvalidAmount);
        long actorId = currentUser.GetCurrentUserId();
        await EnsureVaultPermissionAsync(actorId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        CashProposal proposal = await LockProposalAsync(proposalId, cancellationToken);
        if (proposal.Version != request.ExpectedVersion) throw new ConflictException(MessageCode.ConcurrentModification);
        if (proposal.Status != "PendingApproval") throw new BusinessRuleException(MessageCode.CashHandoffNotPending);
        CashPositionSession vault = await LockSessionAsync(proposal.VaultSessionId, cancellationToken);
        if (!await db.BusinessDates.AnyAsync(day => day.Date == vault.BusinessDate && day.Status == OperationsConstants.BusinessDateOpen && day.Date <= BusinessTime.Today, cancellationToken))
            throw new BusinessRuleException(MessageCode.BusinessDateClosed);
        if (vault.Status != OperationsConstants.CashSessionOpen || vault.ExpectedClosingCash - vault.ReservedCash < request.ApprovedAmount)
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
        vault.ReservedCash += request.ApprovedAmount;
        vault.Version++;
        proposal.ApprovedAmount = request.ApprovedAmount;
        proposal.ApprovedBy = actorId;
        proposal.ApprovedAtUtc = DateTime.UtcNow;
        proposal.Status = "PendingReceipt";
        proposal.Version++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToProposalResponse(proposal);
    }

    public Task<CashProposalResponse> ReceiveCashProposalAsync(long proposalId, CashProposalActionRequest request, CancellationToken cancellationToken) =>
        ResolveCashProposalAsync(proposalId, request, receive: true, cancellationToken);

    public Task<CashProposalResponse> RejectCashProposalAsync(long proposalId, CashProposalActionRequest request, CancellationToken cancellationToken) =>
        ResolveCashProposalAsync(proposalId, request, receive: false, cancellationToken);

    private async Task<CashProposalResponse> ResolveCashProposalAsync(long proposalId, CashProposalActionRequest request, bool receive, CancellationToken cancellationToken)
    {
        TransactionRequestValidator.EnsureMaximumLength(request.Note, 500);
        long actorId = currentUser.GetCurrentUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        CashProposal proposal = await LockProposalAsync(proposalId, cancellationToken);
        if (proposal.Version != request.ExpectedVersion)
            throw new ConflictException(MessageCode.ConcurrentModification);
        if (proposal.Status == "PendingApproval" && !receive)
        {
            if (proposal.OfficerId != actorId && !await HasVaultPermissionAsync(actorId, cancellationToken))
                throw new ForbiddenException(MessageCode.InsufficientPermission);
            proposal.Status = "Rejected";
            proposal.ResolvedAtUtc = DateTime.UtcNow;
            proposal.Version++;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToProposalResponse(proposal);
        }
        if (proposal.OfficerId != actorId || proposal.Status != "PendingReceipt")
            throw new ConflictException(MessageCode.CashHandoffNotPending);
        CashPositionSession vault = await LockSessionAsync(proposal.VaultSessionId, cancellationToken);
        CashPositionSession teller = await LockSessionAsync(proposal.SessionId, cancellationToken);
        if (!await db.BusinessDates.AnyAsync(day => day.Date == teller.BusinessDate && day.Status == OperationsConstants.BusinessDateOpen, cancellationToken))
            throw new BusinessRuleException(MessageCode.BusinessDateClosed);
        decimal amount = proposal.ApprovedAmount!.Value;
        vault.ReservedCash -= amount;
        if (receive)
        {
            if (teller.Status != OperationsConstants.CashSessionOpen || vault.ExpectedClosingCash - amount < vault.ReservedCash)
                throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
            vault.ExpectedClosingCash -= amount;
            teller.ExpectedClosingCash += amount;
            if (proposal.IsInitialCash)
                teller.OpeningCash += amount;
            teller.Version++;
            db.CashMovements.Add(new CashMovement { SessionId = vault.Id, DestinationSessionId = teller.Id,
                Type = "VaultIssue", Amount = amount, ActorId = actorId, ApprovedBy = proposal.ApprovedBy,
                CreatedAtUtc = DateTime.UtcNow, Note = request.Note });
        }
        vault.Version++;
        proposal.Status = receive ? "Received" : "Rejected";
        proposal.ResolvedAtUtc = DateTime.UtcNow;
        proposal.Version++;
        db.CashMovements.Add(new CashMovement { SessionId = teller.Id, DestinationSessionId = vault.Id,
            Type = receive ? "CashReceipt" : "CashProposalRejected", Amount = receive ? amount : 0m,
            ActorId = actorId, CreatedAtUtc = DateTime.UtcNow, Note = request.Note });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToProposalResponse(proposal);
    }

    public async Task<CashPositionSessionDetailResponse> GetSessionDetailAsync(long sessionId, CancellationToken cancellationToken)
    {
        var session = await db.CashPositionSessions.AsNoTracking().Include(item => item.Teller).SingleOrDefaultAsync(item => item.Id == sessionId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        var actorId = currentUser.GetCurrentUserId();
        var canReviewAllCash = await HasVaultPermissionAsync(actorId, cancellationToken) || await HasAuditPermissionAsync(actorId, cancellationToken);
        if ((session.PositionType == OperationsConstants.CashPositionVault || session.TellerId != actorId) && !canReviewAllCash)
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
        var transactionIds = movements.Where(item => item.TransactionId.HasValue).Select(item => item.TransactionId!.Value).Distinct().ToList();
        var transactions = await db.Transactions.AsNoTracking().Where(item => transactionIds.Contains(item.Id))
            .OrderByDescending(item => item.TransactionAt).Select(item => new SessionTransactionResponse(item.Id,
                item.TransactionNo, item.TransactionType.ToString(), item.TransactionStatus.ToString(), item.Amount, item.TransactionAt)).ToListAsync(cancellationToken);
        var requests = await db.CashProposals.AsNoTracking().Where(item => item.SessionId == sessionId).OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
        var day = await db.BusinessDates.AsNoTracking().SingleOrDefaultAsync(item => item.Date == session.BusinessDate, cancellationToken);
        bool manager = await HasVaultPermissionAsync(actorId, cancellationToken);
        bool hasOpen = await db.CashPositionSessions.AnyAsync(item => item.TellerId == actorId && item.BusinessDate == session.BusinessDate && item.Status == OperationsConstants.CashSessionOpen, cancellationToken);
        var response = WithCapabilities(ToResponse(session) with { ScheduledStartingCash = requests.FirstOrDefault(item => item.IsInitialCash)?.ApprovedAmount ?? 0m }, actorId, manager, hasOpen, day);
        return new CashPositionSessionDetailResponse(response, movements, counts, transactions, requests.Select(ToProposalResponse).ToList());
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
        if (source.BusinessDate != await businessDates.GetPostingBusinessDateValueAsync(DateTime.UtcNow, cancellationToken))
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        if (source.PositionType == OperationsConstants.CashPositionVault || destination.PositionType == OperationsConstants.CashPositionVault)
            await EnsureVaultPermissionAsync(actorId, cancellationToken);
        if (destination.Status != "Open" || source.Id == destination.Id || source.BusinessDate != destination.BusinessDate)
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        if (source.ExpectedClosingCash - request.Amount < source.ReservedCash)
            throw new BusinessRuleException(MessageCode.InsufficientBalance);
        var now = DateTime.UtcNow;
        source.ExpectedClosingCash -= request.Amount;
        destination.ExpectedClosingCash += request.Amount;
        source.Version++;
        destination.Version++;
        AddMovement(source, destination, "TransferOut", request.Amount, actorId, now, request.Note);
        AddMovement(destination, source, "TransferIn", request.Amount, actorId, now, request.Note);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(source);
    }

    public async Task<CashCountResponse> SubmitCountAsync(long sessionId, SubmitCashCountRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        var key = NormalizeCashIdempotencyKey(idempotencyKey);
        if (request.ActualAmount < 0m || decimal.Round(request.ActualAmount, 2) != request.ActualAmount)
            throw new ValidationException(MessageCode.InvalidAmount);
        TransactionRequestValidator.EnsureMaximumLength(request.Notes, 2000);
        var actorId = currentUser.GetCurrentUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var session = await LockSessionAsync(sessionId, cancellationToken);
        if (session.PositionType == OperationsConstants.CashPositionVault)
            await EnsureVaultPermissionAsync(actorId, cancellationToken);
        var priorCount = (await db.CashCounts.FromSql(
            $"SELECT * FROM CashCounts WHERE CountedBy = {actorId} AND IdempotencyKey = {key} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();
        if (priorCount is not null)
        {
            var priorHandoff = await db.CashHandoffs.AsNoTracking()
                .FirstOrDefaultAsync(item => item.CashCountId == priorCount.Id, cancellationToken);
            var originalRecipient = priorHandoff is null ? null : await db.CashHandoffHistories.AsNoTracking()
                .Where(item => item.CashHandoffId == priorHandoff.Id).OrderBy(item => item.Id).Select(item => item.RecipientId).FirstOrDefaultAsync(cancellationToken);
            if (priorCount.SessionId != sessionId || priorCount.ActualAmount != request.ActualAmount ||
                priorCount.Notes != request.Notes || (originalRecipient ?? priorHandoff?.RecipientId) != request.HandoffRecipientUserId)
                throw new ConflictException(MessageCode.IdempotencyKeyReused);
            return new CashCountResponse(priorCount.Id, priorCount.SessionId, priorCount.ExpectedAmount,
                priorCount.ActualAmount, priorCount.Difference, priorCount.Difference == 0m ? "Matched" : "Unmatched",
                priorCount.CountedAtUtc, priorHandoff?.Id, priorHandoff?.Status);
        }
        if (session.Version != request.ExpectedSessionVersion)
            throw new ConflictException(MessageCode.ConcurrentModification);
        db.Entry(session).Property(item => item.Version).OriginalValue = request.ExpectedSessionVersion;
        session.Version++;
        EnsureAssignedToActor(session, actorId);
        if (!await db.BusinessDates.AnyAsync(day => day.Date == session.BusinessDate && day.Status == OperationsConstants.BusinessDateOpen && day.Date <= BusinessTime.Today, cancellationToken))
            throw new BusinessRuleException(MessageCode.BusinessDateClosed);
        var closingSession = session.Status == OperationsConstants.CashSessionOpen;
        if (request.HandoffRecipientUserId.HasValue && (request.ActualAmount == 0m || !closingSession))
            throw new ValidationException(MessageCode.InvalidRequest);
        if (closingSession && request.ActualAmount > 0m && !request.HandoffRecipientUserId.HasValue)
            throw new BusinessRuleException(MessageCode.CashHandoffRecipientRequired);
        if (closingSession && await db.CashProposals.AnyAsync(item => item.SessionId == session.Id &&
                (item.Status == "PendingApproval" || item.Status == "PendingReceipt"), cancellationToken))
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        if (closingSession && session.PositionType == OperationsConstants.CashPositionVault &&
            (session.ReservedCash > 0m || await db.CashProposals.AnyAsync(item => item.VaultSessionId == session.Id &&
                (item.Status == "PendingApproval" || item.Status == "PendingReceipt"), cancellationToken)))
            throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        User? recipient = null;
        if (closingSession && request.ActualAmount > 0m)
            {
            recipient = await GetEligibleHandoffRecipientAsync(request.HandoffRecipientUserId!.Value, actorId, cancellationToken);
            await ValidateHandoffDestinationAsync(session, recipient.Id, cancellationToken);
        }
        CashPositionSession? vaultReturnSession = null;
        if (request.ReturnToVault)
        {
            if (!closingSession || request.ActualAmount <= 0m || recipient is null ||
                !await HasVaultPermissionAsync(recipient.Id, cancellationToken))
                throw new ValidationException(MessageCode.InvalidRequest);
            vaultReturnSession = await db.CashPositionSessions.SingleOrDefaultAsync(item =>
                item.BusinessDate == session.BusinessDate && item.PositionType == OperationsConstants.CashPositionVault &&
                item.Status == OperationsConstants.CashSessionOpen, cancellationToken)
                ?? throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        }
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
            Difference = difference, Notes = request.Notes, CountedBy = actorId, CountedAtUtc = now, IdempotencyKey = key
        };
        db.CashCounts.Add(count);
        CashHandoff? handoff = null;
        if (closingSession && request.ActualAmount > 0m)
        {
            handoff = new CashHandoff
            {
                Session = session, CashCount = count, BusinessDate = session.BusinessDate, SenderId = actorId,
                Recipient = recipient, RecipientId = recipient!.Id, Amount = request.ActualAmount,
                DestinationSession = vaultReturnSession,
                Status = OperationsConstants.CashHandoffPendingAcceptance, CreatedAtUtc = now, UpdatedAtUtc = now
            };
            db.CashHandoffs.Add(handoff);
            db.CashHandoffHistories.Add(new CashHandoffHistory
            {
                CashHandoff = handoff, OldStatus = "None", NewStatus = handoff.Status, RecipientId = recipient.Id,
                Note = "Cash handoff created when the session was closed.", ActorId = actorId, CreatedAtUtc = now
            });
        }
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
            difference == 0m ? "Matched" : "Unmatched", now, handoff?.Id, handoff?.Status);
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
        if (session.ExpectedClosingCash + signedAmount < session.ReservedCash)
            throw new BusinessRuleException(MessageCode.InsufficientCashPositionBalance);
        movement.Status = OperationsConstants.CashMovementApproved;
        movement.ApprovedBy = actorId;
        movement.ApprovedAtUtc = DateTime.UtcNow;
        session.ExpectedClosingCash += signedAmount;
        session.Version++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToAdjustmentResponse(movement, signedAmount);
    }

    /// <summary>Lists posted adjustment requests for approval review.</summary>
    public async Task<IReadOnlyList<CashAdjustmentResponse>> GetAdjustmentsAsync(DateOnly? businessDate, string? status, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(status) && status is not (OperationsConstants.CashMovementPendingApproval or OperationsConstants.CashMovementApproved or OperationsConstants.CashMovementRejected))
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

    /// <summary>Rejects a pending cash adjustment with a required reviewer reason.</summary>
    public async Task<CashAdjustmentResponse> RejectAdjustmentAsync(long movementId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        var actorId = currentUser.GetCurrentUserId();
        if (!await HasVaultPermissionAsync(actorId, cancellationToken))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var movement = (await db.CashMovements.FromSql($"SELECT * FROM CashMovements WHERE Id = {movementId} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault() ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (movement.Type != OperationsConstants.CashMovementAdjustment || movement.Status != OperationsConstants.CashMovementPendingApproval || movement.ActorId == actorId)
            throw new BusinessRuleException(MessageCode.CashAdjustmentApprovalRequired);
        movement.Status = OperationsConstants.CashMovementRejected;
        movement.ApprovedBy = actorId;
        movement.ApprovedAtUtc = DateTime.UtcNow;
        movement.Note = $"Rejection: {reason.Trim()}\n{movement.Note}";
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToAdjustmentResponse(movement, await GetCashEffectAsync(movement.CorrectionTransactionId!.Value,
            (await db.CashPositionSessions.Where(item => item.Id == movement.SessionId).Select(item => item.BusinessDate).SingleAsync(cancellationToken)), cancellationToken));
    }

    /// <summary>Returns active users authorized to receive physical cash custody.</summary>
    public async Task<IReadOnlyList<CashHandoffRecipientResponse>> GetHandoffRecipientsAsync(CancellationToken cancellationToken, long? sourceSessionId = null)
    {
        var actorId = currentUser.GetCurrentUserId();
        var recipients = await (from user in db.Users.AsNoTracking()
            where user.Status == UserStatus.Active && user.Id != actorId &&
                (from ur in db.UserRoles join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
                 join p in db.Permissions on rp.PermissionId equals p.Id
                 where ur.UserId == user.Id && (p.Code == SecurityConstants.CashOperations ||
                     p.Code == SecurityConstants.Audit || p.Code == SecurityConstants.EndOfDayApproval)
                 select p.Id).Any()
            orderby user.FullName, user.Username
            select new CashHandoffRecipientResponse(user.Id, user.FullName, user.Username))
            .ToListAsync(cancellationToken);
        if (!sourceSessionId.HasValue) return recipients;
        var source = await db.CashPositionSessions.AsNoTracking().SingleOrDefaultAsync(item => item.Id == sourceSessionId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (source.TellerId != actorId && !await HasVaultPermissionAsync(actorId, cancellationToken) && !await HasAuditPermissionAsync(actorId, cancellationToken))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        List<CashHandoffRecipientResponse> eligible = [];
        foreach (var recipient in recipients)
        {
            if (await HasVaultPermissionAsync(recipient.UserId, cancellationToken) || await HasAuditPermissionAsync(recipient.UserId, cancellationToken) ||
                source.BusinessDate == BusinessTime.Today && await db.CashPositionSessions.AnyAsync(item => item.TellerId == recipient.UserId &&
                    item.BusinessDate == source.BusinessDate && item.PositionType == OperationsConstants.CashPositionTeller && item.Status == OperationsConstants.CashSessionOpen, cancellationToken))
                eligible.Add(recipient);
        }
        return eligible;
    }

    public async Task<IReadOnlyList<CashHandoffResponse>> GetCashHandoffsAsync(DateOnly? businessDate, CancellationToken cancellationToken)
    {
        var actorId = currentUser.GetCurrentUserId();
        var canReviewAll = await HasHandoffReviewPermissionAsync(actorId, cancellationToken);
        var query = db.CashHandoffs.AsNoTracking().Include(item => item.Sender).Include(item => item.Recipient).AsQueryable();
        if (businessDate.HasValue) query = query.Where(item => item.BusinessDate == businessDate.Value);
        if (!canReviewAll) query = query.Where(item => item.SenderId == actorId || item.RecipientId == actorId);
        var items = await query.OrderByDescending(item => item.CreatedAtUtc).ToListAsync(cancellationToken);
        return items.Select(ToHandoffResponse).ToList();
    }

    public async Task<CashHandoffDetailResponse> GetCashHandoffDetailAsync(long handoffId, CancellationToken cancellationToken)
    {
        var actorId = currentUser.GetCurrentUserId();
        var handoff = await db.CashHandoffs.AsNoTracking().Include(item => item.Sender).Include(item => item.Recipient)
            .SingleOrDefaultAsync(item => item.Id == handoffId, cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        if (handoff.SenderId != actorId && handoff.RecipientId != actorId &&
            !await HasHandoffReviewPermissionAsync(actorId, cancellationToken))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        var history = await db.CashHandoffHistories.AsNoTracking().Where(item => item.CashHandoffId == handoffId)
            .OrderBy(item => item.CreatedAtUtc)
            .Select(item => new CashHandoffHistoryResponse(item.Id, item.OldStatus, item.NewStatus,
                item.PreviousRecipientId, item.RecipientId, item.Note, item.ActorId, item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return new CashHandoffDetailResponse(ToHandoffResponse(handoff), history);
    }

    public Task<CashHandoffResponse> AcceptCashHandoffAsync(long handoffId, CashHandoffActionRequest request, CancellationToken cancellationToken) =>
        ChangeHandoffStatusAsync(handoffId, request, OperationsConstants.CashHandoffAccepted, cancellationToken);

    public Task<CashHandoffResponse> DeclineCashHandoffAsync(long handoffId, CashHandoffActionRequest request, CancellationToken cancellationToken) =>
        ChangeHandoffStatusAsync(handoffId, request, OperationsConstants.CashHandoffDeclined, cancellationToken);

    private async Task<CashHandoffResponse> ChangeHandoffStatusAsync(long handoffId, CashHandoffActionRequest request,
        string newStatus, CancellationToken cancellationToken)
    {
        TransactionRequestValidator.EnsureMaximumLength(request.Note, 500);
        if (newStatus == OperationsConstants.CashHandoffDeclined && string.IsNullOrWhiteSpace(request.Note))
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        var actorId = currentUser.GetCurrentUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var handoff = await LockHandoffAsync(handoffId, cancellationToken);
        if (handoff.RecipientId != actorId)
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        if (handoff.Status == newStatus)
        {
            await db.Entry(handoff).Reference(item => item.Sender).LoadAsync(cancellationToken);
            await db.Entry(handoff).Reference(item => item.Recipient).LoadAsync(cancellationToken);
            return ToHandoffResponse(handoff);
        }
        if (handoff.Status != OperationsConstants.CashHandoffPendingAcceptance)
            throw new BusinessRuleException(MessageCode.CashHandoffNotPending);
        if (handoff.Version != request.ExpectedVersion)
            throw new ConflictException(MessageCode.ConcurrentModification);
        if (newStatus == OperationsConstants.CashHandoffAccepted)
        {
            CashPositionSession source = await LockSessionAsync(handoff.SessionId, cancellationToken);
            await ValidateHandoffDestinationAsync(source, actorId, cancellationToken);
            long? destinationSessionId = null;
            // Recompute the destination after reassignment; a previous recipient's vault must not leak through.
            handoff.DestinationSessionId = null;
            // A teller's handoff accepted by a vault-authorized manager is a return to vault,
            // including handoffs created before a vault destination was explicitly attached.
            if (!destinationSessionId.HasValue && source.PositionType == OperationsConstants.CashPositionTeller &&
                await HasVaultPermissionAsync(actorId, cancellationToken))
            {
                destinationSessionId = await db.CashPositionSessions
                    .Where(item => item.BusinessDate == handoff.BusinessDate &&
                        item.PositionType == OperationsConstants.CashPositionVault &&
                        item.Status == OperationsConstants.CashSessionOpen)
                    .Select(item => (long?)item.Id)
                    .SingleOrDefaultAsync(cancellationToken)
                    ?? throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
                handoff.DestinationSessionId = destinationSessionId;
            }

            if (!await HasVaultPermissionAsync(actorId, cancellationToken) && !await HasAuditPermissionAsync(actorId, cancellationToken))
            {
                var receiver = await db.CashPositionSessions.SingleAsync(item => item.TellerId == actorId &&
                    item.BusinessDate == source.BusinessDate && item.PositionType == OperationsConstants.CashPositionTeller &&
                    item.Status == OperationsConstants.CashSessionOpen, cancellationToken);
                receiver = await LockSessionAsync(receiver.Id, cancellationToken);
                if (receiver.Status != OperationsConstants.CashSessionOpen) throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
                receiver.ExpectedClosingCash += handoff.Amount;
                source.ExpectedClosingCash -= handoff.Amount;
                source.Version++;
                receiver.Version++;
                handoff.DestinationSessionId = receiver.Id;
                AddMovement(source, receiver, "TransferOut", handoff.Amount, actorId, DateTime.UtcNow, request.Note);
                AddMovement(receiver, source, "TransferIn", handoff.Amount, actorId, DateTime.UtcNow, request.Note);
            }
            if (destinationSessionId.HasValue)
            {
                await EnsureVaultPermissionAsync(actorId, cancellationToken);
                CashPositionSession destination = await LockSessionAsync(destinationSessionId.Value, cancellationToken);
                if (destination.PositionType != OperationsConstants.CashPositionVault ||
                    destination.BusinessDate != handoff.BusinessDate || destination.Status != OperationsConstants.CashSessionOpen)
                    throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
                source.ExpectedClosingCash -= handoff.Amount;
                destination.ExpectedClosingCash += handoff.Amount;
                source.Version++;
                destination.Version++;
                DateTime movementTime = DateTime.UtcNow;
                db.CashMovements.Add(new CashMovement { SessionId = source.Id, DestinationSessionId = destination.Id,
                    Type = "VaultReturnOut", Amount = handoff.Amount, ActorId = actorId, CreatedAtUtc = movementTime,
                    Note = request.Note ?? "Manager acknowledged teller return to vault." });
                db.CashMovements.Add(new CashMovement { SessionId = destination.Id, DestinationSessionId = source.Id,
                    Type = "VaultReturnIn", Amount = handoff.Amount, ActorId = actorId, CreatedAtUtc = movementTime,
                    Note = request.Note ?? "Manager acknowledged teller return to vault." });
            }
        }
        db.Entry(handoff).Property(item => item.Version).OriginalValue = request.ExpectedVersion;
        var oldStatus = handoff.Status;
        handoff.Status = newStatus;
        handoff.Version++;
        handoff.UpdatedAtUtc = DateTime.UtcNow;
        if (newStatus == OperationsConstants.CashHandoffAccepted) handoff.AcceptedAtUtc = handoff.UpdatedAtUtc;
        else handoff.DeclinedAtUtc = handoff.UpdatedAtUtc;
        db.CashHandoffHistories.Add(new CashHandoffHistory
        {
            CashHandoffId = handoff.Id, OldStatus = oldStatus, NewStatus = newStatus,
            RecipientId = handoff.RecipientId, Note = request.Note, ActorId = actorId, CreatedAtUtc = handoff.UpdatedAtUtc
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await db.Entry(handoff).Reference(item => item.Sender).LoadAsync(cancellationToken);
        await db.Entry(handoff).Reference(item => item.Recipient).LoadAsync(cancellationToken);
        return ToHandoffResponse(handoff);
    }

    public async Task<CashHandoffResponse> ReassignCashHandoffAsync(long handoffId, ReassignCashHandoffRequest request,
        CancellationToken cancellationToken)
    {
        TransactionRequestValidator.EnsureMaximumLength(request.Note, 500);
        var actorId = currentUser.GetCurrentUserId();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var handoff = await LockHandoffAsync(handoffId, cancellationToken);
        if (handoff.Status == OperationsConstants.CashHandoffAccepted)
            throw new BusinessRuleException(MessageCode.CashHandoffNotPending);
        if (handoff.SenderId != actorId && !await HasHandoffReviewPermissionAsync(actorId, cancellationToken))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        var recipient = await GetEligibleHandoffRecipientAsync(request.RecipientUserId, handoff.SenderId, cancellationToken);
        var source = await LockSessionAsync(handoff.SessionId, cancellationToken);
        await ValidateHandoffDestinationAsync(source, recipient.Id, cancellationToken);
        handoff.DestinationSessionId = null;
        if (handoff.Status == OperationsConstants.CashHandoffPendingAcceptance && handoff.RecipientId == recipient.Id)
        {
            await db.Entry(handoff).Reference(item => item.Sender).LoadAsync(cancellationToken);
            await db.Entry(handoff).Reference(item => item.Recipient).LoadAsync(cancellationToken);
            return ToHandoffResponse(handoff);
        }
        if (handoff.Version != request.ExpectedVersion)
            throw new ConflictException(MessageCode.ConcurrentModification);
        db.Entry(handoff).Property(item => item.Version).OriginalValue = request.ExpectedVersion;
        var oldStatus = handoff.Status;
        var oldRecipientId = handoff.RecipientId;
        handoff.RecipientId = recipient.Id;
        handoff.Recipient = recipient;
        handoff.Status = OperationsConstants.CashHandoffPendingAcceptance;
        handoff.Version++;
        handoff.DeclinedAtUtc = null;
        handoff.UpdatedAtUtc = DateTime.UtcNow;
        db.CashHandoffHistories.Add(new CashHandoffHistory
        {
            CashHandoffId = handoff.Id, OldStatus = oldStatus, NewStatus = handoff.Status,
            PreviousRecipientId = oldRecipientId, RecipientId = recipient.Id, Note = request.Note,
            ActorId = actorId, CreatedAtUtc = handoff.UpdatedAtUtc
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await db.Entry(handoff).Reference(item => item.Sender).LoadAsync(cancellationToken);
        return ToHandoffResponse(handoff);
    }

    // Handoffs use the same date-first lock order as counts and proposals.
    private async Task<CashHandoff> LockHandoffAsync(long id, CancellationToken cancellationToken)
    {
        var date = await db.CashHandoffs.AsNoTracking().Where(item => item.Id == id).Select(item => (DateOnly?)item.BusinessDate).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        await LockCashDateAsync(date, cancellationToken);
        return (await db.CashHandoffs.FromSql($"SELECT * FROM CashHandoffs WHERE Id = {id} FOR UPDATE").ToListAsync(cancellationToken))
            .SingleOrDefault() ?? throw new NotFoundException(MessageCode.ResourceNotFound);
    }

    private async Task<User> GetEligibleHandoffRecipientAsync(long recipientId, long senderId, CancellationToken cancellationToken)
    {
        if (recipientId == senderId) throw new ValidationException(MessageCode.InvalidRequest);
        var user = await db.Users.SingleOrDefaultAsync(item => item.Id == recipientId && item.Status == UserStatus.Active, cancellationToken)
            ?? throw new NotFoundException(MessageCode.UserNotFound);
        var eligible = await (from ur in db.UserRoles join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == recipientId && (p.Code == SecurityConstants.CashOperations || p.Code == SecurityConstants.Audit ||
                p.Code == SecurityConstants.EndOfDayApproval)
            select p.Id).AnyAsync(cancellationToken);
        if (!eligible) throw new ForbiddenException(MessageCode.InsufficientPermission);
        return user;
    }

    private async Task<bool> HasHandoffReviewPermissionAsync(long actorId, CancellationToken cancellationToken) =>
        await (from ur in db.UserRoles join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == actorId && (p.Code == SecurityConstants.Audit || p.Code == SecurityConstants.EndOfDayApproval)
            select p.Id).AnyAsync(cancellationToken);

    private static CashHandoffResponse ToHandoffResponse(CashHandoff item) => new(item.Id, item.SessionId,
        item.BusinessDate, item.SenderId, item.Sender?.FullName ?? string.Empty, item.RecipientId,
        item.Recipient?.FullName ?? string.Empty, item.Amount, item.Status, item.CreatedAtUtc, item.AcceptedAtUtc, item.Version, item.DestinationSessionId);

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
        session.Version++;
        db.CashMovements.Add(new CashMovement
        {
            SessionId = session.Id, Type = isDeposit ? "Deposit" : "Withdrawal", Amount = amount,
            Transaction = transaction, ActorId = actorId, CreatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task<DateOnly> GetOpenBusinessDateAsync(CancellationToken cancellationToken)
    {
        var today = BusinessTime.Today;
        var open = await db.BusinessDates.AsNoTracking().Where(item => item.IsActive && item.Status == "Open" && item.Date <= today)
            .OrderByDescending(item => item.Date).Select(item => (DateOnly?)item.Date).FirstOrDefaultAsync(cancellationToken);
        return open ?? BusinessTime.Today;
    }

    // Lock the business date before individual cash records so closing and settlement serialize.
    private async Task LockCashDateAsync(DateOnly date, CancellationToken ct)
    {
        await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE Date = {date} FOR UPDATE").ToListAsync(ct);
    }

    private async Task<CashPositionSession> LockSessionAsync(long id, CancellationToken cancellationToken)
    {
        var date = await db.CashPositionSessions.AsNoTracking().Where(item => item.Id == id).Select(item => (DateOnly?)item.BusinessDate).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        await LockCashDateAsync(date, cancellationToken);
        return (await db.CashPositionSessions.FromSql($"SELECT * FROM CashPositionSessions WHERE Id = {id} FOR UPDATE").ToListAsync(cancellationToken))
            .SingleOrDefault() ?? throw new NotFoundException(MessageCode.ResourceNotFound);
    }

    private async Task<CashProposal> LockProposalAsync(long id, CancellationToken cancellationToken)
    {
        var date = await db.CashProposals.AsNoTracking().Where(item => item.Id == id).Select(item => (DateOnly?)item.Session!.BusinessDate).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        await LockCashDateAsync(date, cancellationToken);
        return (await db.CashProposals.FromSql($"SELECT * FROM CashProposals WHERE Id = {id} FOR UPDATE").ToListAsync(cancellationToken))
            .SingleOrDefault() ?? throw new NotFoundException(MessageCode.ResourceNotFound);
    }

    private async Task ReleaseUnacceptedInitialCashAsync(CashPositionSession teller, DateTime resolvedAtUtc,
        CancellationToken cancellationToken)
    {
        CashProposal? proposal = await db.CashProposals.SingleOrDefaultAsync(item => item.SessionId == teller.Id &&
            item.IsInitialCash && item.Status == "PendingReceipt", cancellationToken);
        if (proposal is null) return;

        CashPositionSession vault = await LockSessionAsync(proposal.VaultSessionId, cancellationToken);
        decimal amount = proposal.ApprovedAmount ?? 0m;
        if (amount > 0m)
        {
            if (vault.ReservedCash < amount)
                throw new BusinessRuleException(MessageCode.InsufficientBalance);
            vault.ReservedCash -= amount;
            vault.Version++;
        }
        proposal.Status = "Rejected";
        proposal.ResolvedAtUtc = resolvedAtUtc;
        proposal.Version++;
    }

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

    private async Task<bool> HasAuditPermissionAsync(long actorId, CancellationToken cancellationToken) =>
        await (from userRole in db.UserRoles
            join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
            join permission in db.Permissions on rolePermission.PermissionId equals permission.Id
            where userRole.UserId == actorId && permission.Code == SecurityConstants.Audit
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

    // Response capabilities are usability hints; mutation methods revalidate under locks.
    private static CashPositionSessionResponse WithCapabilities(CashPositionSessionResponse session, long actorId, bool manager, bool hasOpen, BusinessDate? day)
    {
        bool own = session.TellerId == actorId;
        bool openDate = day?.Status == OperationsConstants.BusinessDateOpen && session.BusinessDate <= BusinessTime.Today;
        bool current = openDate && day!.IsActive && session.BusinessDate == BusinessTime.Today;
        var now = BusinessTime.ToBusinessDateTime(DateTime.UtcNow);
        return session with
        {
            CanAccept = own && current && !hasOpen && session.Status == OperationsConstants.CashSessionScheduled && session.ScheduledStartAtMyanmar <= now && session.ScheduledEndAtMyanmar > now,
            CanRequestCash = own && current && session.Status == OperationsConstants.CashSessionOpen,
            CanClose = openDate && session.Status == OperationsConstants.CashSessionOpen && (own || manager && session.PositionType == OperationsConstants.CashPositionVault),
            CanCancel = manager && openDate && session.Status == OperationsConstants.CashSessionScheduled && session.ActivatedAtUtc == null
        };
    }

    private static CashPositionSessionResponse ToResponse(CashPositionSession session) => new(
        session.Id, session.PositionType, session.TellerId, session.BusinessDate,
        session.OpeningCash, session.ExpectedClosingCash, session.Status, session.Version,
        session.ScheduledStartAtMyanmar, session.ScheduledEndAtMyanmar, session.ActivatedAtUtc, session.ReservedCash, session.Teller?.FullName, session.ClosedAtUtc, session.ClosedBy);

    private async Task<bool> MatchesOpenRequestAsync(CashPositionSession session, OpenCashSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (session.PositionType != request.PositionType || session.TellerId != request.TellerId ||
            session.ScheduledStartAtMyanmar != request.ScheduledStartAtMyanmar ||
            session.ScheduledEndAtMyanmar != request.ScheduledEndAtMyanmar)
            return false;

        if (request.PositionType == OperationsConstants.CashPositionVault)
            return session.OpeningCash == request.OpeningCash;

        return await db.CashProposals.AsNoTracking().AnyAsync(item => item.SessionId == session.Id &&
            item.IsInitialCash && item.ApprovedAmount == request.OpeningCash, cancellationToken);
    }

    private static CashProposalResponse ToProposalResponse(CashProposal proposal) => new(
        proposal.Id, proposal.SessionId, proposal.VaultSessionId, proposal.OfficerId, proposal.RequestedAmount,
        proposal.ApprovedAmount, proposal.Status, proposal.Note, proposal.CreatedAtUtc,
        proposal.ApprovedAtUtc, proposal.ApprovedBy, proposal.ResolvedAtUtc, proposal.Version);

    private static string NormalizeCashIdempotencyKey(string? idempotencyKey)
    {
        var key = TransactionRequestValidator.TrimToNull(idempotencyKey);
        if (key is null) throw new ValidationException(MessageCode.RequiredFieldMissing);
        TransactionRequestValidator.EnsureMaximumLength(key, TransactionConstants.IdempotencyKeyMaximumLength);
        return key;
    }
}
