using System.Globalization;
using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Users;

namespace bams.desktop.Services;

/// <summary>
/// User Management API calls. Transport, token and error translation are handled by <see cref="ApiClient"/>.
/// </summary>
public sealed class UserService : IUserService
{
    // Round-trip format keeps the offset, so the server compares the exact instant the user picked.
    private const string QueryDateFormat = "o";

    private readonly ApiClient _apiClient;

    public UserService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<UserSummaryResponse>> GetUsersAsync(UserListFilter filter, CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<UserSummaryResponse>>(
            ApiConstants.UsersEndpoint + BuildQueryString(filter),
            cancellationToken);
    }

    public Task<UserResponse> GetUserByIdAsync(long id, CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<UserResponse>($"{ApiConstants.UsersEndpoint}/{id}", cancellationToken);
    }

    public Task<IReadOnlyList<RoleResponse>> GetAssignableRolesAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<RoleResponse>>(ApiConstants.UserRolesEndpoint, cancellationToken);
    }

    public Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<CreateUserRequest, UserResponse>(ApiConstants.UsersEndpoint, request, cancellationToken);
    }

    public Task<UserResponse> UpdateUserAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        return _apiClient.PutAsync<UpdateUserRequest, UserResponse>(
            $"{ApiConstants.UsersEndpoint}/{id}",
            request,
            cancellationToken);
    }

    public Task<UserResponse> ResetPasswordAsync(long id, CancellationToken cancellationToken)
    {
        return _apiClient.PostAsync<UserResponse>(
            $"{ApiConstants.UsersEndpoint}/{id}/{ApiConstants.ResetPasswordSegment}",
            cancellationToken);
    }

    public Task DeleteUserAsync(long id, CancellationToken cancellationToken)
    {
        return _apiClient.DeleteAsync($"{ApiConstants.UsersEndpoint}/{id}", cancellationToken);
    }

    // Builds "?search=..&role=..&createdFrom=..&createdBefore=..", skipping empty filters.
    private static string BuildQueryString(UserListFilter filter)
    {
        var parameters = new List<string>();

        AddParameter(parameters, "search", filter.Search);
        AddParameter(parameters, "role", filter.Role);
        AddParameter(parameters, "createdFrom", filter.CreatedFrom?.ToString(QueryDateFormat, CultureInfo.InvariantCulture));
        AddParameter(parameters, "createdBefore", filter.CreatedBefore?.ToString(QueryDateFormat, CultureInfo.InvariantCulture));

        return parameters.Count == 0 ? string.Empty : "?" + string.Join("&", parameters);
    }

    // Adds one URL-encoded name=value pair when the value is not empty.
    private static void AddParameter(List<string> parameters, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
        }
    }
}
