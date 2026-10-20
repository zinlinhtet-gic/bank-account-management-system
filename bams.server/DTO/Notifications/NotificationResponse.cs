namespace bams.server.DTO.Notifications;

public sealed record NotificationResponse(
    long Id,
    string Type,
    string Category,
    string Title,
    string Message,
    string TargetType,
    long? TargetId,
    string? ActorName,
    DateTime CreatedAtUtc);

public sealed record NotificationListResponse(
    int UnreadCount,
    IReadOnlyList<NotificationResponse> Items);
