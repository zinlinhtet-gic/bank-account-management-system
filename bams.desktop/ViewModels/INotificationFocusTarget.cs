namespace bams.desktop.ViewModels;

/// <summary>
/// A page that can open with one record selected, used when a notification is clicked.
/// The shell calls <see cref="FocusOn"/> before the page initializes; the page selects the record
/// once its data has loaded.
/// </summary>
public interface INotificationFocusTarget
{
    /// <summary>
    /// Remembers the record to select after the next load. <paramref name="targetType"/> is a
    /// <see cref="Constants.NotificationTargets"/> value; pages listing two record kinds use it to tell them apart.
    /// </summary>
    void FocusOn(string targetType, long targetId);
}
