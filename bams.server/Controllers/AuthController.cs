using bams.server.DTO.Auth;
using bams.server.DTO.Common;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using bams.server.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthenticationService _authenticationService;

    public AuthController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<ApiMessageResponse<LoginResponse>>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _authenticationService.LoginAsync(request, cancellationToken);
        var apiResponse = ApiMessageResponse<LoginResponse>.FromCode(
            MessageCode.Success,
            response);

        return Ok(apiResponse);
    }

    /// <summary>
    /// Gets the current user's permissions.
    /// </summary>
    [HttpGet("permissions")]
    [Authorize]
    public async Task<ActionResult<ApiMessageResponse<PermissionsResponse>>> GetPermissionsAsync(
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var permissions = await _authenticationService.GetUserPermissionsAsync(userId, cancellationToken);
        var apiResponse = ApiMessageResponse<PermissionsResponse>.FromCode(
            MessageCode.Success,
            permissions);

        return Ok(apiResponse);
    }

    /// <summary>
    /// Changes the current user's password.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiMessageResponse<ChangePasswordResponse>>> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var response = await _authenticationService.ChangePasswordAsync(userId, request, cancellationToken);
        var apiResponse = ApiMessageResponse<ChangePasswordResponse>.FromCode(
            MessageCode.PasswordChangedSuccessfully,
            response);

        return Ok(apiResponse);
    }
}
