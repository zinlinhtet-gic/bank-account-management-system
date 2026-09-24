using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Auth;

namespace bams.desktop.Services;

/// <summary>
/// Handles authentication API calls to the server.
/// Transport and error translation are delegated to <see cref="ApiClient"/>.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly ApiClient _apiClient;

    public AuthenticationService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Sets the JWT token for authenticated requests.
    /// </summary>
    public void SetAuthToken(string token)
    {
        _apiClient.SetBearerToken(token);
    }

    /// <summary>
    /// Removes the JWT token from authenticated requests.
    /// </summary>
    public void ClearAuthToken()
    {
        _apiClient.ClearBearerToken();
    }

    /// <summary>
    /// Authenticates a user with the server and returns login response.
    /// </summary>
    public Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<LoginRequest, LoginResponse>(
            ApiConstants.LoginEndpoint,
            request,
            cancellationToken);
    }

    /// <summary>
    /// Gets the current user's permissions from the server.
    /// </summary>
    public Task<PermissionsResponse> GetPermissionsAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<PermissionsResponse>(
            ApiConstants.PermissionsEndpoint,
            cancellationToken);
    }

    /// <summary>
    /// Changes the user's password.
    /// </summary>
    public Task<ChangePasswordResponse> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<ChangePasswordRequest, ChangePasswordResponse>(
            ApiConstants.ChangePasswordEndpoint,
            request,
            cancellationToken);
    }

    /// <summary>
    /// Keeps the signed-in user shown as online.
    /// </summary>
    public Task SendHeartbeatAsync(CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<bool>(ApiConstants.HeartbeatEndpoint, cancellationToken);
    }

    /// <summary>
    /// Marks the signed-in user offline on the server.
    /// </summary>
    public Task LogoutAsync(CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<bool>(ApiConstants.LogoutEndpoint, cancellationToken);
    }
}
