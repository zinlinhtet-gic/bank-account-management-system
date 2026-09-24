namespace bams.desktop.Constants;

/// <summary>
/// Online-presence timing. The server shows a user as online for 3 minutes after their last heartbeat
/// (<c>UserConstants.OnlinePresenceTimeout</c>), so the heartbeat interval must stay well below that.
/// </summary>
public static class PresenceConstants
{
    /// <summary>How often a signed-in app tells the server the user is still there.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMinutes(1);

    /// <summary>How often the open User Management page refreshes who is online.</summary>
    public static readonly TimeSpan UserListRefreshInterval = TimeSpan.FromSeconds(30);

    /// <summary>How long logout waits for the server before signing out locally anyway.</summary>
    public static readonly TimeSpan LogoutRequestTimeout = TimeSpan.FromSeconds(3);
}
