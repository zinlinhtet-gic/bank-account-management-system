using bams.server.DTO.Users;

namespace bams.server.Services.Interfaces;

public interface IUserService
{
    /// <summary>
    /// Lists users that are not deleted, filtered by the query and sorted by username.
    /// </summary>
    Task<IReadOnlyList<UserSummaryResponse>> GetUsersAsync(UserListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Gets one user that is not deleted, or throws UserNotFound.
    /// </summary>
    Task<UserResponse> GetUserByIdAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the roles a user can be given, with each role's default password.
    /// </summary>
    Task<IReadOnlyList<RoleResponse>> GetAssignableRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates an active user with the role's default password, which must be changed at the first login.
    /// </summary>
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates a user's profile and role.
    /// </summary>
    Task<UserResponse> UpdateUserAsync(long id, UpdateUserRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the user's password back to their role's default and forces a change at the next login.
    /// </summary>
    Task<UserResponse> ResetPasswordAsync(long id, CancellationToken cancellationToken);

    /// <summary>
    /// Soft-deletes a user: the row is kept, but the user is hidden from the list and can no longer sign in.
    /// </summary>
    Task DeleteUserAsync(long id, CancellationToken cancellationToken);
}
