using bams.server.Data;
using bams.server.Constants;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounting;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using bams.server.Utils.Extensions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Controllers;
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

        var userState = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Status, user.MustChangePassword })
            .FirstOrDefaultAsync(cancellationToken);

        // A valid token for a user that no longer exists is treated as unauthenticated.
        if (userState is null)
        {
            throw new UnauthorizedException(MessageCode.AuthenticationRequired);
        }

        // Disabled or deleted users keep no access even if they still hold an unexpired token.
        userState.Status.EnsureCanSignIn();

        // A new or reset user signs in with a published default password, so business endpoints stay closed until
        // the password is changed. The auth endpoints (change-password, permissions, logout) use [Authorize] only.
        if (userState.MustChangePassword)
        {
            throw new ForbiddenException(MessageCode.PasswordChangeRequired);
        }

        if (!await HasAnyPermissionAsync(dbContext, userId, cancellationToken))
        {
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        }

        // Only teller business writes require an open session; unrelated maintenance writes remain available.
        if (IsWriteRequest(httpContext.Request.Method) &&
            RequiresTellerSession(httpContext.Request.Path) &&
            !AllowsWriteWithoutSession(context) &&
            await IsOfficerAsync(dbContext, userId, cancellationToken))
        {
            var businessDates = httpContext.RequestServices.GetRequiredService<IBusinessDateService>();
            var postingDate = await businessDates.GetPostingBusinessDateValueAsync(DateTime.UtcNow, cancellationToken);
            var hasOpenSession = await dbContext.CashPositionSessions.AsNoTracking().AnyAsync(session =>
                session.TellerId == userId && session.PositionType == OperationsConstants.CashPositionTeller &&
                session.BusinessDate == postingDate && session.Status == OperationsConstants.CashSessionOpen,
                cancellationToken);
            if (!hasOpenSession)
                throw new BusinessRuleException(MessageCode.CashSessionNotOpen);
        }

        await next();
    }

    // Treats HTTP methods that can change server state as writes; reads remain available without a cash session.
    private static bool IsWriteRequest(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method) || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    // Financial postings and cash-custody operations use the teller session; account/customer administration does not.
    private static bool RequiresTellerSession(PathString path) =>
        path.StartsWithSegments("/api/transactions", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/cash-operations", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWithSegments("/api/cash-handoffs", StringComparison.OrdinalIgnoreCase);

    // Resolves the exemption from both endpoint metadata and MVC method metadata for consistent routing behavior.
    private static bool AllowsWriteWithoutSession(ActionExecutingContext context) =>
        context.ActionDescriptor.EndpointMetadata.OfType<AllowWithoutOpenCashSessionAttribute>().Any() ||
        context.ActionDescriptor is ControllerActionDescriptor action &&
        Attribute.IsDefined(action.MethodInfo, typeof(AllowWithoutOpenCashSessionAttribute));

    // Resolves the user's role from persisted authorization data rather than trusting client-supplied role text.
    private static Task<bool> IsOfficerAsync(ApplicationDbContext dbContext, long userId, CancellationToken cancellationToken) =>
        dbContext.UserRoles.AsNoTracking().Where(userRole => userRole.UserId == userId)
            .AnyAsync(userRole => userRole.Role != null && userRole.Role.Code == SecurityConstants.OfficerRole, cancellationToken);

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
