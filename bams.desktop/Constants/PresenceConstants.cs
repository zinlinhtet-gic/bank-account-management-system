namespace bams.desktop.Constants;

/// <summary>
/// Online-presence timing. The server shows a user as online for 45 seconds after their last heartbeat
/// (<c>UserConstants.OnlinePresenceTimeout</c>), so the heartbeat interval must stay well below that
/// (currently it allows two missed heartbeats). Change both sides together.
/// </summary>
public static class PresenceConstants
{
    /// <summary>How often a signed-in app tells the server the user is still there.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

    /// <summary>
    /// How often the open User Management page refreshes who is online. This is the longest a logout can take
    /// to show in the table (opening a user's card updates their row at once).
    /// </summary>
    public static readonly TimeSpan UserListRefreshInterval = TimeSpan.FromSeconds(10);

    /// <summary>How long logout waits for the server before signing out locally anyway.</summary>
    public static readonly TimeSpan LogoutRequestTimeout = TimeSpan.FromSeconds(3);
}
