using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounting;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Provides the persisted bank-wide business date, initializing the first open day when needed.</summary>
public sealed class BusinessDateService(ApplicationDbContext db, ICurrentUserService currentUser) : IBusinessDateService
{
    public async Task<BusinessDateResponse> GetCurrentBusinessDateAsync(CancellationToken cancellationToken)
    {
        var businessDate = await GetOrCreateOpenDateAsync(BusinessTime.Today, cancellationToken);
        return new BusinessDateResponse(businessDate.Date, businessDate.Status, businessDate.OpenedAtUtc, businessDate.ClosedAtUtc);
    }

    public async Task<DateOnly> GetOpenBusinessDateValueAsync(CancellationToken cancellationToken) =>
        (await GetOrCreateOpenDateAsync(BusinessTime.Today, cancellationToken)).Date;

    /// <summary>Returns the active date for a financial posting and prevents posting into yesterday after midnight until EOD closes it.</summary>
    public async Task<DateOnly> GetPostingBusinessDateValueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        return (await GetOrCreateOpenDateAsync(BusinessTime.ToBusinessDate(nowUtc), cancellationToken)).Date;
    }

    private async Task<Models.Accounting.BusinessDate> GetOrCreateOpenDateAsync(DateOnly currentDate, CancellationToken cancellationToken)
    {
        if (currentDate > BusinessTime.Today)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

        var open = await db.BusinessDates.FirstOrDefaultAsync(item =>
            item.Date == currentDate && item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
        if (open is not null) return open;

        var currentDateIsClosed = await db.BusinessDates.AnyAsync(item =>
            item.Date == currentDate && item.Status == OperationsConstants.BusinessDateClosed, cancellationToken);
        if (currentDateIsClosed) throw new BusinessRuleException(MessageCode.BusinessDateClosed);
        var yesterdayIsNotClosed = await db.BusinessDates.AnyAsync(item =>
            item.Date == currentDate.AddDays(-1) && item.Status != OperationsConstants.BusinessDateClosed, cancellationToken);
        if (yesterdayIsNotClosed) throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        open = await db.BusinessDates.FirstOrDefaultAsync(item =>
            item.Date == currentDate && item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
        if (open is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return open;
        }

        currentDateIsClosed = await db.BusinessDates.AnyAsync(item =>
            item.Date == currentDate && item.Status == OperationsConstants.BusinessDateClosed, cancellationToken);
        if (currentDateIsClosed) throw new BusinessRuleException(MessageCode.BusinessDateClosed);
        yesterdayIsNotClosed = await db.BusinessDates.AnyAsync(item =>
            item.Date == currentDate.AddDays(-1) && item.Status != OperationsConstants.BusinessDateClosed, cancellationToken);
        if (yesterdayIsNotClosed) throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

        open = new Models.Accounting.BusinessDate
        {
            Date = currentDate, Status = OperationsConstants.BusinessDateOpen,
            OpenedBy = await GetActorIdAsync(cancellationToken), OpenedAtUtc = DateTime.UtcNow
        };
        db.BusinessDates.Add(open);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return open;
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            open = await db.BusinessDates.AsNoTracking().FirstOrDefaultAsync(item =>
                item.Date == currentDate && item.Status == OperationsConstants.BusinessDateOpen, cancellationToken);
            if (open is not null) return open;
            throw;
        }
    }

    // Scheduled callers without an HTTP identity use only the dedicated disabled system actor.
    private async Task<long> GetActorIdAsync(CancellationToken cancellationToken)
    {
        try { return currentUser.GetCurrentUserId(); }
        catch (AuthenticationRequiredException)
        {
            return await db.Users.AsNoTracking()
                .Where(user => user.Username == ScheduledJobConstants.SystemActorUsername && user.Status == UserStatus.Disabled)
                .Select(user => (long?)user.Id).SingleOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException(MessageCode.AuditUserUnavailable);
        }
    }
}
