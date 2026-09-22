using System.Security.Claims;
using bams.server.Data;
using bams.server.Models.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace bams.server.Services;

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<PermissionHandler> _logger;

    public PermissionHandler(
        ApplicationDbContext dbContext,
        ILogger<PermissionHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        try
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("User ID claim not found or invalid");
                return;
            }

            var userRoleIds = await _dbContext.UserRoles
                .AsNoTracking()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            if (!userRoleIds.Any())
            {
                _logger.LogWarning("User {UserId} has no roles assigned", userId);
                return;
            }

            var permissionIds = await _dbContext.RolePermissions
                .AsNoTracking()
                .Where(rp => userRoleIds.Contains(rp.RoleId))
                .Select(rp => rp.PermissionId)
                .ToListAsync();

            var hasPermission = await _dbContext.Permissions
                .AsNoTracking()
                .Where(p => permissionIds.Contains(p.Id) && p.Code == requirement.Permission.ToLower())
                .AnyAsync();

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
            else
            {
                _logger.LogWarning("User {UserId} does not have permission {Permission}", userId, requirement.Permission);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission {Permission}", requirement.Permission);
        }
    }
}