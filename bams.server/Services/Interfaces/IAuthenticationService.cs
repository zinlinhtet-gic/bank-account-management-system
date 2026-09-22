using bams.server.DTO.Auth;

namespace bams.server.Services.Interfaces;

public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user and returns a JWT token if credentials are valid.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the permissions for a user based on their roles.
    /// </summary>
    Task<PermissionsResponse> GetUserPermissionsAsync(long userId, CancellationToken cancellationToken);
}