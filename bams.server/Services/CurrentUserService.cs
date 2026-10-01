using System.Security.Claims;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Services.Interfaces;

namespace bams.server.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public long GetCurrentUserId()
    {
        var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        // Audit attribution must come from a valid authenticated identity.
        if (!long.TryParse(userIdClaim, out var userId) || userId <= 0)
        {
            throw new AuthenticationRequiredException(MessageCode.AuthenticationRequired);
        }

        return userId;
    }
}
