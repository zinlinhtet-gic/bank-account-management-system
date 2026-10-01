using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Users;

namespace bams.desktop.Services;

/// <summary>
/// User Management API calls. Transport, token and error translation are handled by <see cref="ApiClient"/>.
/// </summary>
public sealed class UserService : IUserService
{
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
        return new QueryString()
            .Add("search", filter.Search)
            .Add("role", filter.Role)
            .Add("createdFrom", filter.CreatedFrom)
            .Add("createdBefore", filter.CreatedBefore)
            .ToString();
    }
}
