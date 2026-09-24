using bams.server.Constants;
using bams.server.DTO.Common;
using bams.server.DTO.Users;
using bams.server.Messages;
using bams.server.Middlewares;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

/// <summary>
/// Staff user management. Every action requires the <c>user_management</c> permission (managers).
/// </summary>
[ApiController]
[Route("api/users")]
[RequirePermission(SecurityConstants.UserManagement)]
public sealed class UsersController : ControllerBase
{
    private const string GetUserByIdRouteName = "GetUserById";

    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Lists users (not deleted), sorted by username. Optional filters: search, role, createdFrom, createdBefore.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<UserSummaryResponse>>>> GetUsersAsync(
        [FromQuery] UserListQuery query,
        CancellationToken cancellationToken)
    {
        var users = await _userService.GetUsersAsync(query, cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<UserSummaryResponse>>.FromCode(MessageCode.Success, users));
    }

    /// <summary>
    /// Lists the roles that can be assigned, with their default passwords.
    /// </summary>
    [HttpGet("roles")]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<RoleResponse>>>> GetAssignableRolesAsync(
        CancellationToken cancellationToken)
    {
        var roles = await _userService.GetAssignableRolesAsync(cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<RoleResponse>>.FromCode(MessageCode.Success, roles));
    }

    /// <summary>
    /// Gets one user's full detail.
    /// </summary>
    [HttpGet("{id:long}", Name = GetUserByIdRouteName)]
    public async Task<ActionResult<ApiMessageResponse<UserResponse>>> GetUserByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var user = await _userService.GetUserByIdAsync(id, cancellationToken);

        return Ok(ApiMessageResponse<UserResponse>.FromCode(MessageCode.Success, user));
    }

    /// <summary>
    /// Creates a user with the role's default password.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiMessageResponse<UserResponse>>> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.CreateUserAsync(request, cancellationToken);
        var response = ApiMessageResponse<UserResponse>.FromCode(MessageCode.UserCreatedSuccessfully, user);

        return CreatedAtRoute(GetUserByIdRouteName, new { id = user.Id }, response);
    }

    /// <summary>
    /// Updates a user's profile and role.
    /// </summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<ApiMessageResponse<UserResponse>>> UpdateUserAsync(
        long id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var user = await _userService.UpdateUserAsync(id, request, cancellationToken);

        return Ok(ApiMessageResponse<UserResponse>.FromCode(MessageCode.UserUpdatedSuccessfully, user));
    }

    /// <summary>
    /// Resets the user's password to their role's default.
    /// </summary>
    [HttpPost("{id:long}/reset-password")]
    public async Task<ActionResult<ApiMessageResponse<UserResponse>>> ResetPasswordAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var user = await _userService.ResetPasswordAsync(id, cancellationToken);

        return Ok(ApiMessageResponse<UserResponse>.FromCode(MessageCode.UserPasswordResetSuccessfully, user));
    }

    /// <summary>
    /// Soft-deletes a user.
    /// </summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteUserAsync(
        long id,
        CancellationToken cancellationToken)
    {
        await _userService.DeleteUserAsync(id, cancellationToken);

        return NoContent();
    }
}
