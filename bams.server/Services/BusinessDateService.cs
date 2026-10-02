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
        var businessDate = await GetOrCreateOpenDateAsync(cancellationToken);
        return new BusinessDateResponse(businessDate.Date, businessDate.Status, businessDate.OpenedAtUtc, businessDate.ClosedAtUtc);
    }

    public async Task<DateOnly> GetOpenBusinessDateValueAsync(CancellationToken cancellationToken) =>
        (await GetOrCreateOpenDateAsync(cancellationToken)).Date;

    /// <summary>Returns the active date for a financial posting and prevents posting into yesterday after midnight until EOD closes it.</summary>
    public async Task<DateOnly> GetPostingBusinessDateValueAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var currentDate = BusinessTime.ToBusinessDate(nowUtc);
        var openDates = await db.BusinessDates
            .FromSql($"SELECT * FROM BusinessDates WHERE Status = {OperationsConstants.BusinessDateOpen} ORDER BY Date DESC FOR UPDATE")
            .ToListAsync(cancellationToken);
        var openDate = openDates.FirstOrDefault();
        if (openDate is not null && (openDate.Date == currentDate || openDate.Date > currentDate))
            return openDate.Date;

        var currentDateIsOpen = openDates.Any(item => item.Date == currentDate);
        var yesterdayIsNotClosed = await db.BusinessDates.AsNoTracking().AnyAsync(item =>
            item.Date == currentDate.AddDays(-1) && item.Status != OperationsConstants.BusinessDateClosed, cancellationToken);
        if (yesterdayIsNotClosed && !currentDateIsOpen)
            throw new BusinessRuleException(MessageCode.BusinessDateTransitionConflict);

        if (openDate is not null)
            return openDate.Date;

        var currentDateIsClosed = await db.BusinessDates.AsNoTracking().AnyAsync(item =>
            item.Date == currentDate && item.Status == OperationsConstants.BusinessDateClosed, cancellationToken);
        if (currentDateIsClosed)
            throw new BusinessRuleException(MessageCode.BusinessDateClosed);

        return currentDate;
    }

    private async Task<Models.Accounting.BusinessDate> GetOrCreateOpenDateAsync(CancellationToken cancellationToken)
    {
        var open = await db.BusinessDates.Where(item => item.Status == OperationsConstants.BusinessDateOpen)
            .OrderByDescending(item => item.Date).FirstOrDefaultAsync(cancellationToken);
        if (open is not null) return open;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        open = await db.BusinessDates.Where(item => item.Status == OperationsConstants.BusinessDateOpen)
            .OrderByDescending(item => item.Date).FirstOrDefaultAsync(cancellationToken);
        if (open is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return open;
        }

        open = new Models.Accounting.BusinessDate
        {
            Date = BusinessTime.Today, Status = OperationsConstants.BusinessDateOpen,
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
            open = await db.BusinessDates.AsNoTracking().Where(item => item.Status == OperationsConstants.BusinessDateOpen)
                .OrderByDescending(item => item.Date).FirstOrDefaultAsync(cancellationToken);
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
