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
    private const string BaseUrl = "http://localhost:5121/api/auth"; // TODO: Move to configuration

    public AuthenticationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
                $"{BaseUrl}/login",
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
                $"{BaseUrl}/permissions",
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

    // API response wrapper to match server response format
    private record ApiResponse<T>(
        int Code,
        string Name,
        string Message,
        T Data);
}