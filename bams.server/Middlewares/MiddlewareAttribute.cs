using System.Security.Claims;
using bams.server.Data;
using bams.server.Models.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Middlewares;

/// <summary>
/// Attribute to apply permission checking to endpoints.
/// Usage: [RequirePermission("account_management")] or [RequirePermission("user_list", "user_create")]
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string[] _permissions;

    public RequirePermissionAttribute(params string[] permissions)
    {
        _permissions = permissions;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userIdClaim = context.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !long.TryParse(userIdClaim.Value, out var userId))
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                success = false,
                code = "AUTHENTICATION_REQUIRED",
                message = "Authentication required"
            });
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var hasAnyPermission = await HasAnyPermissionAsync(dbContext, userId, _permissions);
        
        if (!hasAnyPermission)
        {
            context.Result = new ObjectResult(new
            {
                success = false,
                code = "PERMISSION_DENIED",
                message = "Insufficient permissions"
            })
            {
                StatusCode = 403
            };
            return;
        }

        await next();
    }

    private async Task<bool> HasAnyPermissionAsync(ApplicationDbContext dbContext, long userId, string[] permissions)
    {
        var userRoleIds = await dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync();

        if (!userRoleIds.Any())
        {
            return false;
        }

        var permissionIds = await dbContext.RolePermissions
            .Where(rp => userRoleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionId)
            .ToListAsync();

        var availablePermissions = await dbContext.Permissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Code)
            .ToListAsync();

        return permissions.Any(p => availablePermissions.Contains(p.ToLower()));
    }
}