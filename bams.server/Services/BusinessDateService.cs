using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Provides the manager-started, persisted bank-wide business date.</summary>
public sealed class BusinessDateService(ApplicationDbContext db, ICurrentUserService currentUser, CashSessionSchedulingService scheduling) : IBusinessDateService
{
    public async Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken)
    {
        var currentDate = BusinessTime.Today;
        bool canStart = !await db.BusinessDates.AnyAsync(item => item.Date == currentDate, cancellationToken);
        var businessDate = await db.BusinessDates.AsNoTracking().Where(item => item.IsActive && item.Date <= currentDate && item.Status == OperationsConstants.BusinessDateOpen)
            .OrderBy(item => item.Date).FirstOrDefaultAsync(cancellationToken);
        if (businessDate is null)
            businessDate = await db.BusinessDates.AsNoTracking().SingleOrDefaultAsync(item => item.Date == currentDate, cancellationToken);
        return businessDate is null
            ? new BusinessDateResponse(currentDate, "NotStarted", DateTime.MinValue, null, false, canStart, currentDate)
            : new BusinessDateResponse(businessDate.Date, businessDate.Status, businessDate.OpenedAtUtc, businessDate.ClosedAtUtc, businessDate.IsActive, canStart, currentDate);
    }

    public async Task<DateOnly> GetOpenBusinessDateValueAsync(CancellationToken cancellationToken) =>
        await db.BusinessDates.AsNoTracking().Where(item => item.IsActive && item.Status == OperationsConstants.BusinessDateOpen && item.Date <= BusinessTime.Today)
            .OrderBy(item => item.Date).Select(item => (DateOnly?)item.Date).FirstOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

    /// <summary>Uses the source date while open; corrections after force close post on the active open date.</summary>
    public async Task<DateOnly> GetCorrectionPostingDateAsync(DateOnly sourceDate, CancellationToken cancellationToken)
    {
        BusinessDate? source = await db.BusinessDates.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Date == sourceDate, cancellationToken);
        if (source?.Status == OperationsConstants.BusinessDateOpen)
            return sourceDate;

        bool wasForceClosed = source?.Status == OperationsConstants.BusinessDateClosed &&
            await db.EndOfDayRuns.AsNoTracking().AnyAsync(run => run.BusinessDate == sourceDate &&
                run.Status == OperationsConstants.EodClosed && run.OverrideReason != null && run.OverrideReason != "", cancellationToken);
        if (!wasForceClosed)
            throw new BusinessRuleException(MessageCode.BusinessDateClosed);

        DateOnly postingDate = await GetOpenBusinessDateValueAsync(cancellationToken);
        if (postingDate <= sourceDate)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        return postingDate;
    }

    /// <summary>Returns today's explicitly opened date and blocks posting until a manager starts it.</summary>
    public async Task<DateOnly> GetPostingBusinessDateValueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var currentDate = BusinessTime.ToBusinessDate(nowUtc);
        var openDate = await db.BusinessDates.AsNoTracking().SingleOrDefaultAsync(item =>
            item.IsActive && item.Date == currentDate && item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
        if (openDate is null)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        return currentDate;
    }

    public async Task<BusinessDateResponse> StartCurrentBusinessDateAsync(StartBusinessDateRequest request, CancellationToken cancellationToken)
    {
        if (!request.StartingCash.HasValue)
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        if (request.StartingCash.Value < 0m || decimal.Round(request.StartingCash.Value, 2) != request.StartingCash.Value)
            throw new ValidationException(MessageCode.InvalidAmount);

        var key = TransactionRequestValidator.TrimToNull(request.IdempotencyKey);
        if (key is not null) TransactionRequestValidator.EnsureMaximumLength(key, TransactionConstants.IdempotencyKeyMaximumLength - 12);
        var date = BusinessTime.Today;
        if (request.ExpectedDate.HasValue && request.ExpectedDate != date) throw new ConflictException(MessageCode.BusinessDateTransitionConflict);
        key ??= $"BDVAULT-{date:yyyyMMdd}";
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize date switches; the generated unique key also protects an empty-table race.
        var activeDates = await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE IsActive = 1 FOR UPDATE").ToListAsync(cancellationToken);
        var existing = await db.BusinessDates.SingleOrDefaultAsync(item => item.Date == date, cancellationToken);
        if (existing is not null)
            return await ReplayOpeningAsync(existing, request, key, cancellationToken);
        foreach (var prior in activeDates) { prior.IsActive = false; prior.Version++; }
        await db.SaveChangesAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var businessDate = new Models.Accounting.BusinessDate
        {
            Date = date, Status = OperationsConstants.BusinessDateOpen, IsActive = true,
            OpenedBy = currentUser.GetCurrentUserId(), OpenedAtUtc = now
        };
        db.BusinessDates.Add(businessDate);
        var vault = new CashPositionSession
        {
            PositionType = OperationsConstants.CashPositionVault,
            BusinessDate = date,
            OpeningCash = request.StartingCash.Value,
            ExpectedClosingCash = request.StartingCash.Value,
            Status = OperationsConstants.CashSessionOpen,
            OpenedAtUtc = now,
            OpenedBy = businessDate.OpenedBy,
            OpenIdempotencyKey = key
        };
        db.CashPositionSessions.Add(vault);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var concurrentStart = await db.BusinessDates.AsNoTracking().SingleOrDefaultAsync(item => item.Date == date, cancellationToken);
            if (concurrentStart is not null)
                return await ReplayOpeningAsync(concurrentStart, request, key, cancellationToken);
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        }
        await scheduling.CreateAsync(request.Sessions ?? [], vault, businessDate.OpenedBy, key, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildOpeningResponseAsync(businessDate, cancellationToken);
    }

    /// <summary>Returns a retry only when the actor, vault cash and complete schedule inputs match.</summary>
    private async Task<BusinessDateResponse> ReplayOpeningAsync(BusinessDate day, StartBusinessDateRequest request, string key, CancellationToken ct)
    {
        var vault = await db.CashPositionSessions.AsNoTracking().SingleOrDefaultAsync(item => item.BusinessDate == day.Date && item.PositionType == OperationsConstants.CashPositionVault, ct);
        if (vault is null || vault.OpenedBy != currentUser.GetCurrentUserId() || vault.OpenIdempotencyKey != key ||
            vault.OpeningCash != request.StartingCash || day.Status != OperationsConstants.BusinessDateOpen)
            throw new ConflictException(MessageCode.BusinessDateTransitionConflict);
        var rows = request.Sessions ?? [];
        var created = await db.CashPositionSessions.AsNoTracking().Where(item => item.BusinessDate == day.Date && item.OpenedBy == vault.OpenedBy &&
            item.OpenIdempotencyKey != null && item.OpenIdempotencyKey.StartsWith(key + ":")).OrderBy(item => item.Id).ToListAsync(ct);
        if (created.Count != rows.Count) throw new ConflictException(MessageCode.IdempotencyKeyReused);
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index]; var session = created[index];
            if (session.TellerId != row.OfficerId || session.ScheduledStartAtMyanmar != row.StartAtMyanmar || session.ScheduledEndAtMyanmar != row.EndAtMyanmar ||
                !await db.CashProposals.AnyAsync(item => item.SessionId == session.Id && item.IsInitialCash && item.ApprovedAmount == row.StartingCash, ct))
                throw new ConflictException(MessageCode.IdempotencyKeyReused);
        }
        return await BuildOpeningResponseAsync(day, ct);
    }

    /// <summary>Supplies the opening result together with the date's vault and officer sessions.</summary>
    private async Task<BusinessDateResponse> BuildOpeningResponseAsync(BusinessDate day, CancellationToken ct)
    {
        var sessions = await db.CashPositionSessions.AsNoTracking().Include(item => item.Teller).Where(item => item.BusinessDate == day.Date).OrderBy(item => item.Id)
            .Select(item => new CashPositionSessionResponse(item.Id, item.PositionType, item.TellerId, item.BusinessDate, item.OpeningCash,
                item.ExpectedClosingCash, item.Status, item.Version, item.ScheduledStartAtMyanmar, item.ScheduledEndAtMyanmar, item.ActivatedAtUtc,
                item.ReservedCash, item.Teller == null ? null : item.Teller.FullName, item.ClosedAtUtc, item.ClosedBy)).ToListAsync(ct);
        return new BusinessDateResponse(day.Date, day.Status, day.OpenedAtUtc, day.ClosedAtUtc, day.IsActive, false, BusinessTime.Today)
        { Vault = sessions.Single(item => item.PositionType == OperationsConstants.CashPositionVault), Sessions = sessions.Where(item => item.PositionType == OperationsConstants.CashPositionTeller).ToList() };
    }

}
