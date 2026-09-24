using bams.desktop.DTOs.Users;

namespace bams.desktop.Services;

/// <summary>
/// User Management API calls (<c>api/users</c>). All of them need the <c>user_management</c> permission.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Loads the users matching the filter, sorted by username. Deleted users are never returned.
    /// </summary>
    Task<IReadOnlyList<UserSummaryResponse>> GetUsersAsync(UserListFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// Loads one user's full detail.
    /// </summary>
    Task<UserResponse> GetUserByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the roles a user can be given, with each role's default password.
    /// </summary>
    Task<IReadOnlyList<RoleResponse>> GetAssignableRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a user and returns the saved record.
    /// </summary>
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a user and returns the saved record.
    /// </summary>
    Task<UserResponse> UpdateUserAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Resets the user's password to their role's default.
    /// </summary>
    Task<UserResponse> ResetPasswordAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deletes the user.
    /// </summary>
    Task DeleteUserAsync(long id, CancellationToken cancellationToken);
}
