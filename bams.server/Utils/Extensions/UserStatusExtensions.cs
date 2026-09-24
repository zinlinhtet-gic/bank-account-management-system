using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Security;

namespace bams.server.Utils.Extensions;

public static class UserStatusExtensions
{
    /// <summary>
    /// Rejects a user who is not allowed to sign in or call protected endpoints.
    /// Used by login, the auth endpoints and <c>[RequirePermission]</c> so all of them answer the same way.
    /// </summary>
    /// <exception cref="ForbiddenException">
    /// <c>UserAccountDeleted</c> for a soft-deleted user, <c>UserAccountDisabled</c> for any other non-active status.
    /// </exception>
    public static void EnsureCanSignIn(this UserStatus status)
    {
        if (status == UserStatus.Deleted)
        {
            throw new ForbiddenException(MessageCode.UserAccountDeleted);
        }

        if (status != UserStatus.Active)
        {
            throw new ForbiddenException(MessageCode.UserAccountDisabled);
        }
    }
}
