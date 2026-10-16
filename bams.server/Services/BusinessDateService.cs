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
public sealed class BusinessDateService(ApplicationDbContext db, ICurrentUserService currentUser) : IBusinessDateService
{
    public async Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken)
    {
        var currentDate = BusinessTime.Today;
        var businessDate = await db.BusinessDates.AsNoTracking().Where(item => item.IsActive && item.Date <= currentDate && item.Status == OperationsConstants.BusinessDateOpen)
            .OrderBy(item => item.Date).FirstOrDefaultAsync(cancellationToken);
        if (businessDate is null)
            businessDate = await db.BusinessDates.AsNoTracking().SingleOrDefaultAsync(item => item.Date == currentDate, cancellationToken);
        return businessDate is null
            ? new BusinessDateResponse(currentDate, "NotStarted", DateTime.MinValue, null, false, true)
            : new BusinessDateResponse(businessDate.Date, businessDate.Status, businessDate.OpenedAtUtc, businessDate.ClosedAtUtc, businessDate.IsActive, businessDate.Date < currentDate);
    }

    public async Task<DateOnly> GetOpenBusinessDateValueAsync(CancellationToken cancellationToken) =>
        await db.BusinessDates.AsNoTracking().Where(item => item.IsActive && item.Status == OperationsConstants.BusinessDateOpen && item.Date <= BusinessTime.Today)
            .OrderBy(item => item.Date).Select(item => (DateOnly?)item.Date).FirstOrDefaultAsync(cancellationToken)
        ?? throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

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

        var date = BusinessTime.Today;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize date switches; the generated unique key also protects an empty-table race.
        var activeDates = await db.BusinessDates.FromSql($"SELECT * FROM BusinessDates WHERE IsActive = 1 FOR UPDATE").ToListAsync(cancellationToken);
        if (await db.BusinessDates.AnyAsync(item => item.Date == date, cancellationToken))
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        foreach (var prior in activeDates) { prior.IsActive = false; prior.Version++; }
        await db.SaveChangesAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var businessDate = new Models.Accounting.BusinessDate
        {
            Date = date, Status = OperationsConstants.BusinessDateOpen, IsActive = true,
            OpenedBy = currentUser.GetCurrentUserId(), OpenedAtUtc = now
        };
        db.BusinessDates.Add(businessDate);
        db.CashPositionSessions.Add(new CashPositionSession
        {
            PositionType = OperationsConstants.CashPositionVault,
            BusinessDate = date,
            OpeningCash = request.StartingCash.Value,
            ExpectedClosingCash = request.StartingCash.Value,
            Status = OperationsConstants.CashSessionOpen,
            OpenedAtUtc = now,
            OpenedBy = businessDate.OpenedBy,
            OpenIdempotencyKey = $"BDVAULT-{date:yyyyMMdd}"
        });
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var concurrentStart = await db.BusinessDates.AsNoTracking().SingleOrDefaultAsync(item => item.Date == date, cancellationToken);
            bool vaultExists = await db.CashPositionSessions.AsNoTracking().AnyAsync(session =>
                session.BusinessDate == date && session.PositionType == OperationsConstants.CashPositionVault &&
                session.Status == OperationsConstants.CashSessionOpen, cancellationToken);
            if (concurrentStart?.Status == OperationsConstants.BusinessDateOpen && vaultExists)
                return new BusinessDateResponse(date, concurrentStart.Status, concurrentStart.OpenedAtUtc, concurrentStart.ClosedAtUtc, concurrentStart.IsActive);
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);
        }
        await transaction.CommitAsync(cancellationToken);
        return new BusinessDateResponse(date, businessDate.Status, now, null, true);
    }

}
