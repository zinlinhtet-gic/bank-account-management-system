namespace bams.server.Services.Interfaces;

public interface ICurrentUserService
{
    /// <summary>
    /// Gets the authenticated user's identifier from the current request.
    /// </summary>
    long GetCurrentUserId();
}
