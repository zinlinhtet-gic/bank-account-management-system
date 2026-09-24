using bams.server.Data;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Security;
using bams.server.Utils.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Middlewares;

/// <summary>
/// Attribute to apply permission checking to endpoints.
/// Usage: [RequirePermission("account_management")] or [RequirePermission("user_list", "user_create")]
/// Failures are thrown as application exceptions so the global exception handler
/// returns the standard <c>ApiErrorResponse</c> contract.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute : Attribute, IAsyncActionFilter
{
    private readonly string[] _permissions;

    public RequirePermissionAttribute(params string[] permissions)
    {
        // Permission codes are stored in lowercase, so normalize once at construction.
        _permissions = permissions.Select(permission => permission.ToLowerInvariant()).ToArray();
    }

    // Rejects the request unless the caller is an active user holding at least one required permission.
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var userId = httpContext.User.GetRequiredUserId();
        var dbContext = httpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var cancellationToken = httpContext.RequestAborted;

        var userStatus = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (UserStatus?)user.Status)
            .FirstOrDefaultAsync(cancellationToken);

        // A valid token for a user that no longer exists is treated as unauthenticated.
        if (userStatus is null)
        {
            throw new UnauthorizedException(MessageCode.AuthenticationRequired);
        }

        // Disabled or deleted users keep no access even if they still hold an unexpired token.
        userStatus.Value.EnsureCanSignIn();

        if (!await HasAnyPermissionAsync(dbContext, userId, cancellationToken))
        {
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        }

        await next();
    }

    // Checks, in a single query, whether any of the user's roles grants one of the required permissions.
    private async Task<bool> HasAnyPermissionAsync(
        ApplicationDbContext dbContext,
        long userId,
        CancellationToken cancellationToken)
    {
        return await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Join(
                dbContext.RolePermissions,
                userRole => userRole.RoleId,
                rolePermission => rolePermission.RoleId,
                (userRole, rolePermission) => rolePermission.PermissionId)
            .Join(
                dbContext.Permissions,
                permissionId => permissionId,
                permission => permission.Id,
                (permissionId, permission) => permission.Code)
            .AnyAsync(code => _permissions.Contains(code), cancellationToken);
    }
}
