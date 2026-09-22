using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using bams.desktop.DTOs.Auth;
using bams.desktop.Exceptions;
using bams.desktop.Utils;

namespace bams.desktop.Services;

/// <summary>
/// Handles authentication API calls to the server.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly HttpClient _httpClient;
    private const string AuthPath = "api/auth";
    private string? _authToken;

    public AuthenticationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Sets the JWT token for authenticated requests.
    /// </summary>
    public void SetAuthToken(string token)
    {
        _authToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Authenticates a user with the server and returns login response.
    /// </summary>
    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{AuthPath}/login",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<LoginResponse>>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResponse?.Data == null)
            {
                throw new ApiException("Invalid response from server");
            }

            return apiResponse.Data;
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkException("Unable to connect to server", ex);
        }
        catch (Exception ex)
        {
            throw new ApiException($"Server error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the current user's permissions from the server.
    /// </summary>
    public async Task<PermissionsResponse> GetPermissionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{AuthPath}/permissions",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<PermissionsResponse>>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResponse?.Data == null)
            {
                throw new ApiException("Invalid response from server");
            }

            return apiResponse.Data;
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkException("Unable to connect to server", ex);
        }
        catch (Exception ex)
        {
            throw new ApiException($"Server error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Changes the user's password.
    /// </summary>
    public async Task<ChangePasswordResponse> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{AuthPath}/change-password",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<ChangePasswordResponse>>(
                content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (apiResponse?.Data == null)
            {
                throw new ApiException("Invalid response from server");
            }

            return apiResponse.Data;
        }
        catch (HttpRequestException ex)
        {
            throw new NetworkException("Unable to connect to server", ex);
        }
        catch (Exception ex)
        {
            throw new ApiException($"Server error: {ex.Message}", ex);
        }
    }

    // API response wrapper to match server response format
    private record ApiResponse<T>(
        int Code,
        string Name,
        string Message,
        T Data);
}