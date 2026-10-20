using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Notifications;
using bams.server.Models.Notifications;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services.Notifications;

public sealed class NotificationService(ApplicationDbContext db, ICurrentUserService currentUser) : INotificationService
{
    private const string SystemActorName = "System";

    /// <inheritdoc />
    public async Task NotifyUsersAsync(IEnumerable<long?> recipientIds, long? actorUserId, NotificationMessage message,
        CancellationToken cancellationToken)
    {
        var ids = recipientIds.Where(id => id.HasValue && id != actorUserId).Select(id => id!.Value).Distinct().ToList();
        if (ids.Count == 0) return;

        // Inactive users would never read the row, so they are skipped.
        var activeIds = await db.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id) && user.Status == UserStatus.Active)
            .Select(user => user.Id).ToListAsync(cancellationToken);
        StageNotifications(activeIds, actorUserId, message);
    }

    /// <inheritdoc />
    public async Task NotifyRoleAsync(string roleCode, long? actorUserId, NotificationMessage message,
        CancellationToken cancellationToken)
    {
        var ids = await (from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join role in db.Roles on userRole.RoleId equals role.Id
            where user.Status == UserStatus.Active && role.Code == roleCode
            select user.Id).Distinct().ToListAsync(cancellationToken);
        StageNotifications(ids.Where(id => id != actorUserId), actorUserId, message);
    }

    /// <inheritdoc />
    public async Task NotifyPermissionHoldersAsync(IReadOnlyCollection<string> permissionCodes, long? actorUserId,
        NotificationMessage message, CancellationToken cancellationToken)
    {
        var ids = await GetPermissionHolderIdsAsync(permissionCodes, cancellationToken);
        StageNotifications(ids.Where(id => id != actorUserId), actorUserId, message);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<long>> GetPermissionHolderIdsAsync(IReadOnlyCollection<string> permissionCodes,
        CancellationToken cancellationToken) =>
        await (from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles on user.Id equals userRole.UserId
            join rolePermission in db.RolePermissions on userRole.RoleId equals rolePermission.RoleId
            join permission in db.Permissions on rolePermission.PermissionId equals permission.Id
            where user.Status == UserStatus.Active && permissionCodes.Contains(permission.Code)
            select user.Id).Distinct().ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task DismissGroupAsync(string groupKey, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var unread = await db.Notifications
            .Where(notification => notification.GroupKey == groupKey && notification.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        // Rows staged earlier in the same unit of work are not in the database yet.
        unread.AddRange(db.Notifications.Local.Where(notification =>
            notification.GroupKey == groupKey && notification.ReadAtUtc == null && notification.Id == 0));
        foreach (var notification in unread)
            notification.ReadAtUtc = now;
    }

    /// <inheritdoc />
    public async Task<string> GetUserNameAsync(long? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue) return SystemActorName;
        var user = await db.Users.AsNoTracking().Where(item => item.Id == userId.Value)
            .Select(item => new { item.FullName, item.Username }).SingleOrDefaultAsync(cancellationToken);
        return user is null ? SystemActorName : string.IsNullOrWhiteSpace(user.FullName) ? user.Username : user.FullName;
    }

    /// <inheritdoc />
    public async Task<NotificationListResponse> GetUnreadAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.GetCurrentUserId();
        var unread = db.Notifications.AsNoTracking()
            .Where(notification => notification.RecipientUserId == userId && notification.ReadAtUtc == null);
        var count = await unread.CountAsync(cancellationToken);
        var items = await (from notification in unread
            join actor in db.Users on notification.ActorUserId equals actor.Id into actors
            from actor in actors.DefaultIfEmpty()
            orderby notification.CreatedAtUtc descending, notification.Id descending
            select new NotificationResponse(notification.Id, notification.Type, notification.Category,
                notification.Title, notification.Message, notification.TargetType, notification.TargetId,
                actor == null ? null : actor.FullName, notification.CreatedAtUtc))
            .Take(NotificationConstants.MaximumUnreadReturned)
            .ToListAsync(cancellationToken);
        return new NotificationListResponse(count, items);
    }

    /// <inheritdoc />
    public async Task MarkReadAsync(long notificationId, CancellationToken cancellationToken)
    {
        var userId = currentUser.GetCurrentUserId();
        // Scoped to the recipient so a user can never clear someone else's notification.
        await db.Notifications
            .Where(notification => notification.Id == notificationId && notification.RecipientUserId == userId &&
                notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAtUtc, DateTime.UtcNow),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.GetCurrentUserId();
        await db.Notifications
            .Where(notification => notification.RecipientUserId == userId && notification.ReadAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAtUtc, DateTime.UtcNow),
                cancellationToken);
    }

    // Adds one row per recipient to the shared context; the caller's SaveChanges persists them.
    private void StageNotifications(IEnumerable<long> recipientIds, long? actorUserId, NotificationMessage message)
    {
        var now = DateTime.UtcNow;
        foreach (var recipientId in recipientIds)
        {
            db.Notifications.Add(new Notification
            {
                RecipientUserId = recipientId,
                ActorUserId = actorUserId,
                Type = message.Type,
                Category = message.Category,
                Title = Truncate(message.Title, NotificationConstants.MaximumTitleLength),
                Message = Truncate(message.Message, NotificationConstants.MaximumMessageLength),
                TargetType = message.TargetType,
                TargetId = message.TargetId,
                GroupKey = message.GroupKey,
                CreatedAtUtc = now
            });
        }
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];
}
