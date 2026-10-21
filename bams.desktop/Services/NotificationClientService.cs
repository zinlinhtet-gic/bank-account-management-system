using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Notifications;

namespace bams.desktop.Services;

public sealed class NotificationClientService(ApiClient apiClient) : INotificationClientService
{
    /// <inheritdoc />
    public Task<NotificationListResponse> GetUnreadAsync(CancellationToken cancellationToken) =>
        apiClient.GetAsync<NotificationListResponse>(ApiConstants.NotificationsEndpoint, cancellationToken);

    /// <inheritdoc />
    public Task MarkReadAsync(long notificationId, CancellationToken cancellationToken) =>
        apiClient.PostAsync<bool>(
            $"{ApiConstants.NotificationsEndpoint}/{notificationId}/{ApiConstants.NotificationReadSegment}", cancellationToken);

    /// <inheritdoc />
    public Task MarkAllReadAsync(CancellationToken cancellationToken) =>
        apiClient.PostAsync<bool>(ApiConstants.NotificationsReadAllEndpoint, cancellationToken);
}
