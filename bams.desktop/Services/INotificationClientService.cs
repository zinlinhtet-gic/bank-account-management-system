using bams.desktop.DTOs.Notifications;

namespace bams.desktop.Services;

/// <summary>Reads and clears the signed-in user's notifications on the server.</summary>
public interface INotificationClientService
{
    /// <summary>Returns the newest unread notifications and the total unread count.</summary>
    Task<NotificationListResponse> GetUnreadAsync(CancellationToken cancellationToken);

    /// <summary>Marks one notification as read so it leaves the bell.</summary>
    Task MarkReadAsync(long notificationId, CancellationToken cancellationToken);

    /// <summary>Marks every notification of the user as read.</summary>
    Task MarkAllReadAsync(CancellationToken cancellationToken);
}
