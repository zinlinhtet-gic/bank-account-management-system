namespace bams.desktop.DTOs.Notifications;

/// <summary>One unread notification for the signed-in user.</summary>
public sealed record NotificationResponse(
    long Id,
    string Type,
    string Category,
    string Title,
    string Message,
    string TargetType,
    long? TargetId,
    string? ActorName,
    DateTime CreatedAtUtc)
{
    // Shown in the bell list; the server stores UTC.
    public DateTime CreatedAtLocal => DateTime.SpecifyKind(CreatedAtUtc, DateTimeKind.Utc).ToLocalTime();
}

/// <summary>The newest unread notifications plus the total unread count for the badge.</summary>
public sealed record NotificationListResponse(
    int UnreadCount,
    IReadOnlyList<NotificationResponse> Items);
