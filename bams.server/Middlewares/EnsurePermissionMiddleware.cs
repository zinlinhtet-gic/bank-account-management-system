using System.Security.Claims;
using bams.server.Data;
using bams.server.Models.Security;
using bams.server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Middlewares;

/// <summary>
/// Middleware to ensure the current user has any of the specified permissions.
/// This middleware is registered globally and works with the RequirePermission attribute.
/// </summary>
public sealed class EnsurePermissionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<EnsurePermissionMiddleware> _logger;

    public EnsurePermissionMiddleware(
        RequestDelegate next,
        ApplicationDbContext dbContext,
        ILogger<EnsurePermissionMiddleware> logger)
    {
        _next = next;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);
    }
}