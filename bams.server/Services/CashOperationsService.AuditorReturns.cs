using System.Text.Json;
using bams.server.Constants;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed partial class CashOperationsService
{
    // Limits transfer submission to the Auditor role as well as the audit permission.
    private async Task EnsureAuditorTransferAccessAsync(CancellationToken ct)
    {
        var actor = currentUser.GetCurrentUserId();
        if (!await HasAuditPermissionAsync(actor, ct) || !await (from ur in db.UserRoles
            join role in db.Roles on ur.RoleId equals role.Id
            where ur.UserId == actor && role.Code == SecurityConstants.AuditorRole select ur.UserId).AnyAsync(ct))
            throw new ForbiddenException(MessageCode.InsufficientPermission);
    }

    // Offers only active recipients authorized to receive cash into the vault.
    public async Task<IReadOnlyList<CashHandoffRecipientResponse>> GetAuditorReturnManagersAsync(CancellationToken ct)
    {
        await EnsureAuditorTransferAccessAsync(ct);
        var recipients = await GetHandoffRecipientsAsync(ct);
        var managers = new List<CashHandoffRecipientResponse>();
        foreach (var recipient in recipients)
            if (await HasVaultPermissionAsync(recipient.UserId, ct)) managers.Add(recipient);
        return managers;
    }
    // Lists custody returns visible to their participants and authorized reviewers.
    public async Task<IReadOnlyList<AuditorCashReturnResponse>> GetAuditorReturnsAsync(DateOnly? date, CancellationToken ct)
    {
        var actor = currentUser.GetCurrentUserId();
        var review = await HasHandoffReviewPermissionAsync(actor, ct);
        var rows = await db.AuditorCashReturns.AsNoTracking().Where(x =>
            (!date.HasValue || x.BusinessDate == date) && (review || x.AuditorId == actor || x.ManagerId == actor))
            .OrderByDescending(x => x.Id).ToListAsync(ct);
        return rows.Select(ToAuditorReturnResponse).ToList();
    }

    // Reserves whole accepted handoffs without changing physical custody or cash balances.
    public async Task<AuditorCashReturnResponse> CreateAuditorReturnAsync(CreateAuditorCashReturnRequest request, CancellationToken ct)
    {
        var actor = currentUser.GetCurrentUserId();
        await EnsureAuditorTransferAccessAsync(ct);
        if (request.HandoffIds is null || request.HandoffIds.Count == 0 || request.HandoffIds.Distinct().Count() != request.HandoffIds.Count || actor == request.ManagerId)
            throw new ValidationException(MessageCode.InvalidRequest);
        TransactionRequestValidator.EnsureMaximumLength(request.Note, 500);
        await GetEligibleHandoffRecipientAsync(request.ManagerId, actor, ct);
        await EnsureVaultPermissionAsync(request.ManagerId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var dates = await db.CashHandoffs.Where(x => request.HandoffIds.Contains(x.Id)).Select(x => x.BusinessDate).Distinct().ToListAsync(ct);
        if (dates.Count != 1) throw new ValidationException(MessageCode.InvalidRequest);
        var first = await LockHandoffAsync(request.HandoffIds.Min(), ct);
        await EnsureReturnDateOpenAsync(first.BusinessDate, ct);
        var pending = await db.AuditorCashReturns.Where(x => x.BusinessDate == first.BusinessDate && x.Status == "PendingAcceptance").ToListAsync(ct);
        var reserved = pending.SelectMany(x => JsonSerializer.Deserialize<long[]>(x.HandoffIdsJson)!).ToHashSet();
        foreach (var id in request.HandoffIds.Order())
        {
            var h = await LockHandoffAsync(id, ct);
            if (h.BusinessDate != first.BusinessDate || h.RecipientId != actor || h.Status != OperationsConstants.CashHandoffAccepted || h.DestinationSessionId.HasValue || reserved.Contains(id))
                throw new BusinessRuleException(MessageCode.InvalidRequest);
        }
        var row = new AuditorCashReturn { BusinessDate = first.BusinessDate, AuditorId = actor, ManagerId = request.ManagerId,
            HandoffIdsJson = JsonSerializer.Serialize(request.HandoffIds), Note = request.Note, CreatedAtUtc = DateTime.UtcNow };
        db.AuditorCashReturns.Add(row);
        await db.SaveChangesAsync(ct);
        await NotifyAuditorReturnAsync(row, actor, true, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToAuditorReturnResponse(row);
    }

    // Manager receipt posts the original closed sources into the vault exactly once.
    public async Task<AuditorCashReturnResponse> DecideAuditorReturnAsync(long id, CashHandoffActionRequest request, bool accept, CancellationToken ct)
    {
        var actor = currentUser.GetCurrentUserId();
        await EnsureVaultPermissionAsync(actor, ct);
        TransactionRequestValidator.EnsureMaximumLength(request.Note, 500);
        if (!accept && string.IsNullOrWhiteSpace(request.Note)) throw new ValidationException(MessageCode.RequiredFieldMissing);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var date = await db.AuditorCashReturns.Where(x => x.Id == id).Select(x => (DateOnly?)x.BusinessDate).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException(MessageCode.ResourceNotFound);
        await LockCashDateAsync(date, ct);
        await EnsureReturnDateOpenAsync(date, ct);
        var row = await db.AuditorCashReturns.SingleAsync(x => x.Id == id, ct);
        if (row.ManagerId != actor) throw new ForbiddenException(MessageCode.InsufficientPermission);
        var status = accept ? "Accepted" : "Declined";
        if (row.Status == status) return ToAuditorReturnResponse(row);
        if (row.Status != "PendingAcceptance" || row.Version != request.ExpectedVersion) throw new ConflictException(MessageCode.ConcurrentModification);
        if (accept)
        {
            var vaultId = await db.CashPositionSessions.Where(x => x.BusinessDate == date && x.PositionType == OperationsConstants.CashPositionVault && x.Status == OperationsConstants.CashSessionOpen)
                .Select(x => (long?)x.Id).SingleOrDefaultAsync(ct) ?? throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
            var vault = await LockSessionAsync(vaultId, ct);
            foreach (var handoffId in JsonSerializer.Deserialize<long[]>(row.HandoffIdsJson)!.Order())
            {
                var h = await LockHandoffAsync(handoffId, ct);
                if (h.RecipientId != row.AuditorId || h.Status != OperationsConstants.CashHandoffAccepted || h.DestinationSessionId.HasValue)
                    throw new ConflictException(MessageCode.ConcurrentModification);
                var source = await LockSessionAsync(h.SessionId, ct);
                if (source.Status != OperationsConstants.CashSessionClosed || source.ExpectedClosingCash < h.Amount)
                    throw new BusinessRuleException(MessageCode.InvalidRequest);
                source.ExpectedClosingCash -= h.Amount;
                vault.ExpectedClosingCash += h.Amount;
                source.Version++;
                vault.Version++;
                AddMovement(source, vault, "VaultReturnOut", h.Amount, actor, DateTime.UtcNow, request.Note);
                AddMovement(vault, source, "VaultReturnIn", h.Amount, actor, DateTime.UtcNow, request.Note);
                h.DestinationSessionId = vault.Id;
                h.Version++;
                h.UpdatedAtUtc = DateTime.UtcNow;
                db.CashHandoffHistories.Add(new CashHandoffHistory { CashHandoffId = h.Id,
                    OldStatus = h.Status, NewStatus = h.Status, RecipientId = actor, ActorId = actor,
                    Note = $"Manager received auditor return #{row.Id}. {request.Note}", CreatedAtUtc = DateTime.UtcNow });
            }
        }
        row.Status = status;
        row.DecisionNote = request.Note;
        row.DecidedAtUtc = DateTime.UtcNow;
        row.Version++;
        await NotifyAuditorReturnAsync(row, actor, false, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToAuditorReturnResponse(row);
    }

    // Stages notifications in the same transaction as the custody decision.
    private Task NotifyAuditorReturnAsync(AuditorCashReturn row, long actor, bool submitted, CancellationToken ct) =>
        notifications.NotifyUsersAsync([submitted ? row.ManagerId : row.AuditorId], actor,
            new Notifications.NotificationMessage(submitted ? NotificationConstants.Types.HandoffReceived : NotificationConstants.Types.HandoffAccepted,
                NotificationConstants.Categories.CashManagement, submitted ? "Auditor cash return awaiting receipt" : "Auditor cash return decided",
                $"Return #{row.Id} for {row.BusinessDate:yyyy-MM-dd}: {row.Status}. Review Auditor Cash Returns in Session Handoff Requests.",
                NotificationConstants.Targets.CashHandoff, System.Text.Json.JsonSerializer.Deserialize<long[]>(row.HandoffIdsJson)![0]), ct);

    // Cash custody cannot be changed after the business date is closed.
    private async Task EnsureReturnDateOpenAsync(DateOnly date, CancellationToken ct)
    {
        if (!await db.BusinessDates.AnyAsync(x => x.Date == date && x.Status == OperationsConstants.BusinessDateOpen, ct))
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
    }

    // Maps persisted custody metadata to the public response.
    private static AuditorCashReturnResponse ToAuditorReturnResponse(AuditorCashReturn x) => new(x.Id, x.BusinessDate,
        x.AuditorId, x.ManagerId, JsonSerializer.Deserialize<long[]>(x.HandoffIdsJson)!, x.Status, x.Note, x.DecisionNote,
        x.CreatedAtUtc, x.DecidedAtUtc, x.Version);
}
