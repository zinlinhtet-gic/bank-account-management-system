namespace bams.server.Models.Notifications;

/// <summary>
/// One in-app notification addressed to a single user. Rows are created inside the same
/// transaction as the business action that caused them and are hidden once read.
/// </summary>
public sealed class Notification
{
    public long Id { get; set; }

    public long RecipientUserId { get; set; }

    // Who caused the notification; null for scheduled (system) actions.
    public long? ActorUserId { get; set; }

    // Stable machine-readable kind, see NotificationConstants.Types.
    public string Type { get; set; } = string.Empty;

    // Feature area shown in the bell, e.g. "Cash Management" or "Reconciliation".
    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    // Where the desktop navigates when the notification is clicked, see NotificationConstants.Targets.
    public string TargetType { get; set; } = string.Empty;

    public long? TargetId { get; set; }

    // Shared by every copy of one actionable request so all copies can be dismissed together.
    public string? GroupKey { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }
}
