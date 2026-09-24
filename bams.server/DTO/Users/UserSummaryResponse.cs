namespace bams.server.DTO.Users;

/// <summary>
/// One row of the User Management list.
/// </summary>
/// <param name="Role">Role code, e.g. "manager".</param>
/// <param name="IsOnline">Signed in and seen within <c>UserConstants.OnlinePresenceTimeout</c>.</param>
/// <param name="LastSeenAt">Last login or heartbeat (UTC); null if the user never signed in.</param>
public sealed record UserSummaryResponse(
    long Id,
    string Username,
    string FullName,
    string Email,
    string Role,
    DateTime CreatedAt,
    bool IsOnline,
    DateTime? LastSeenAt);
