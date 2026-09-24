using System.ComponentModel;
using bams.desktop.Constants;
using bams.desktop.DTOs.Users;
using bams.desktop.Utils;

namespace bams.desktop.Models;

/// <summary>
/// One row of the User Management table, shaped for binding. Presence is updated in place by the page's
/// periodic refresh, so it notifies; the other fields are fixed for the row's lifetime.
/// </summary>
public sealed class UserDisplayModel : INotifyPropertyChanged
{
    private bool _isOnline;
    private DateTime? _lastSeenAt;

    public event PropertyChangedEventHandler? PropertyChanged;

    public long Id { get; init; }

    public string Username { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RoleCode { get; init; } = string.Empty;

    public string RoleName => RoleCodes.ToDisplayName(RoleCode);

    /// <summary>Local creation time.</summary>
    public DateTime CreatedAt { get; init; }

    public string CreatedAtText => CreatedAt.ToString(DisplayFormats.Date);

    /// <summary>True for the signed-in user's own row (shows a "You" tag).</summary>
    public bool IsCurrentUser { get; init; }

    /// <summary>Signed in and active within the last few minutes (decided by the server).</summary>
    public bool IsOnline => _isOnline;

    /// <summary>"Online" / "Offline", shown in the status badge.</summary>
    public string PresenceText => IsOnline ? "Online" : "Offline";

    /// <summary>Tooltip on the status badge, e.g. "Last seen 12 min ago".</summary>
    public string LastSeenText => PresenceFormatter.FormatLastSeen(_lastSeenAt, IsOnline);

    /// <summary>
    /// Builds a row from the server list item.
    /// </summary>
    public static UserDisplayModel FromResponse(UserSummaryResponse user, long? currentUserId)
    {
        var row = new UserDisplayModel
        {
            Id = user.Id,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            RoleCode = user.Role,
            CreatedAt = DateTimeDisplay.ToLocal(user.CreatedAt),
            IsCurrentUser = user.Id == currentUserId
        };
        row.UpdatePresence(user.IsOnline, user.LastSeenAt);

        return row;
    }

    /// <summary>
    /// Applies fresh presence from the server without rebuilding the row.
    /// </summary>
    public void UpdatePresence(bool isOnline, DateTime? lastSeenAtUtc)
    {
        _isOnline = isOnline;
        _lastSeenAt = lastSeenAtUtc is null ? null : DateTimeDisplay.ToLocal(lastSeenAtUtc.Value);

        OnPropertyChanged(nameof(IsOnline));
        OnPropertyChanged(nameof(PresenceText));
        OnPropertyChanged(nameof(LastSeenText));
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
