using System.Security.Claims;
using bams.server.Models.Security;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Middlewares;

/// <summary>
/// Middleware to ensure the current user's role has the required feature permission.
/// Usage: [Middleware("feature:user_management")]
/// </summary>
public sealed class EnsureFeatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<EnsureFeatureMiddleware> _logger;

    public EnsureFeatureMiddleware(
        RequestDelegate next,
        ILogger<EnsureFeatureMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requiredFeature = context.Items["feature"] as string;
        
        if (string.IsNullOrEmpty(requiredFeature))
        {
            await _next(context);
            return;
        }

        var roleClaim = context.User.FindFirst(ClaimTypes.Role);
        if (roleClaim == null)
        {
            await UnauthorizedResponse(context, "Authentication required");
            return;
        }

        var role = roleClaim.Value.ToLower();
        var hasFeature = HasFeature(role, requiredFeature);

        if (!hasFeature)
        {
            _logger.LogWarning("Role {Role} does not have feature {Feature}", role, requiredFeature);
            await UnauthorizedResponse(context, $"Feature '{requiredFeature}' is not available for your role");
            return;
        }

        await _next(context);
    }

    private bool HasFeature(string role, string feature)
    {
        // Define feature mappings based on roles
        var roleFeatures = new Dictionary<string, HashSet<string>>
        {
            ["manager"] = new HashSet<string>
            {
                "user_management",
                "customer_management",
                "customer_kyc",
                "accounting",
                "configuration",
                "operation"
            },
            ["officer"] = new HashSet<string>
            {
                "customer_management",
                "account_management",
                "transactions"
            },
            ["auditor"] = new HashSet<string>
            {
                "customer_list",
                "accounting",
                "transaction_history",
                "audit"
            }
        };

        return roleFeatures.TryGetValue(role, out var features) && features.Contains(feature.ToLower());
    }

    private static async Task UnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = 403;
        context.Response.ContentType = "application/json";
        
        var response = new
        {
            success = false,
            code = "FEATURE_NOT_AVAILABLE",
            message = message
        };
        
        await context.Response.WriteAsJsonAsync(response);
    }
}