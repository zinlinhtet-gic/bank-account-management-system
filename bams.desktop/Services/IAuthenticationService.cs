using bams.desktop.DTOs.Auth;

namespace bams.desktop.Services;

public interface IAuthenticationService
{
    /// <summary>
    /// Authenticates a user with the server and returns login response.
    /// </summary>
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current user's permissions from the server.
    /// </summary>
    Task<PermissionsResponse> GetPermissionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sets the JWT token for authenticated requests.
    /// </summary>
    void SetAuthToken(string token);

    /// <summary>
    /// Removes the JWT token from authenticated requests.
    /// </summary>
    void ClearAuthToken();

    /// <summary>
    /// Changes the user's password.
    /// </summary>
    Task<ChangePasswordResponse> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Tells the server the signed-in user is still using the app, so they stay shown as online.
    /// </summary>
    Task SendHeartbeatAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Tells the server the user signed out, so they are shown as offline at once.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken);
}