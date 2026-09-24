using bams.server.Constants;
using bams.server.DTO.Users;
using bams.server.Models.Security;

namespace bams.server.Mapping;

public static class UserMappings
{
    // Converts a User entity (with UserRoles.Role loaded) into the detailed API response contract.
    // A user has one role in practice; login and permissions also read the first one.
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse(
            user.Id,
            user.Username,
            user.FullName,
            user.Email,
            user.Phone,
            user.UserRoles.FirstOrDefault()?.Role?.Code ?? string.Empty,
            user.Status,
            user.LastLoginAt,
            user.MustChangePassword,
            user.CreatedAt,
            user.UpdatedAt,
            IsOnline(user.OnlineStatus, user.LastSeenAt, DateTime.UtcNow),
            user.LastSeenAt);
    }

    /// <summary>
    /// The single online rule: signed in (not logged out) and seen within <see cref="UserConstants.OnlinePresenceTimeout"/>.
    /// A missing heartbeat (app closed, PC offline) therefore turns into "offline" on its own.
    /// </summary>
    public static bool IsOnline(OnlineStatus onlineStatus, DateTime? lastSeenAtUtc, DateTime nowUtc)
    {
        return onlineStatus == OnlineStatus.Active
            && lastSeenAtUtc is not null
            && nowUtc - lastSeenAtUtc.Value <= UserConstants.OnlinePresenceTimeout;
    }
}
