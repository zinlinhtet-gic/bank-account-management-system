using bams.server.DTO.Notifications;

namespace bams.server.Services.Notifications;

/// <summary>Content of one notification before it is addressed to recipients.</summary>
public sealed record NotificationMessage(
    string Type,
    string Category,
    string Title,
    string Message,
    string TargetType,
    long? TargetId,
    string? GroupKey = null);

/// <summary>
/// Creates and reads in-app notifications. Write methods only stage rows on the shared
/// DbContext, so they commit or roll back with the caller's business transaction.
/// </summary>
public interface INotificationService
{
    /// <summary>Stages one notification per distinct active recipient, never addressed to the actor.</summary>
    Task NotifyUsersAsync(IEnumerable<long?> recipientIds, long? actorUserId, NotificationMessage message, CancellationToken cancellationToken);

    /// <summary>Stages a notification for every active user holding the role.</summary>
    Task NotifyRoleAsync(string roleCode, long? actorUserId, NotificationMessage message, CancellationToken cancellationToken);

    /// <summary>Stages a notification for every active user holding any of the permissions.</summary>
    Task NotifyPermissionHoldersAsync(IReadOnlyCollection<string> permissionCodes, long? actorUserId, NotificationMessage message, CancellationToken cancellationToken);

    /// <summary>Returns active users holding any of the permissions, for combining with other recipients.</summary>
    Task<IReadOnlyList<long>> GetPermissionHolderIdsAsync(IReadOnlyCollection<string> permissionCodes, CancellationToken cancellationToken);

    /// <summary>Marks every unread copy of a request as read once someone has acted on it.</summary>
    Task DismissGroupAsync(string groupKey, CancellationToken cancellationToken);

    /// <summary>Returns the user's display name for notification text.</summary>
    Task<string> GetUserNameAsync(long? userId, CancellationToken cancellationToken);

    /// <summary>Returns the current user's unread notifications, newest first.</summary>
    Task<NotificationListResponse> GetUnreadAsync(CancellationToken cancellationToken);

    /// <summary>Marks one of the current user's notifications as read.</summary>
    Task MarkReadAsync(long notificationId, CancellationToken cancellationToken);

    /// <summary>Marks all of the current user's notifications as read.</summary>
    Task MarkAllReadAsync(CancellationToken cancellationToken);
}
