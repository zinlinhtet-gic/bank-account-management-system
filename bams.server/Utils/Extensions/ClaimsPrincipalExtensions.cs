using System.Security.Claims;
using bams.server.Exceptions;
using bams.server.Messages;

namespace bams.server.Utils.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Reads the authenticated user's identifier from the JWT name-identifier claim.
    /// </summary>
    /// <exception cref="UnauthorizedException">
    /// Thrown when the request is unauthenticated or the claim is missing or malformed.
    /// </exception>
    public static long GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            throw new UnauthorizedException(MessageCode.AuthenticationRequired);
        }

        return userId;
    }
}
