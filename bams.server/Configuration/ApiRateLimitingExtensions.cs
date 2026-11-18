using System.Globalization;
using System.Threading.RateLimiting;
using bams.server.DTO.Common;
using bams.server.Messages;

namespace bams.server.Configuration;

/// <summary>Registers API and login throttles using the standard API error envelope.</summary>
public static class ApiRateLimitingExtensions
{
    // Refuse invalid budgets at startup and queue no requests on a small VPS.
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(ApiRateLimitOptions.SectionName).Get<ApiRateLimitOptions>()
            ?? new ApiRateLimitOptions();
        if (settings.ApiRequestsPerMinute <= 0 || settings.LoginRequestsPerMinute <= 0)
            throw new InvalidOperationException("RateLimiting request budgets must be positive.");

        services.AddRateLimiter(options =>
        {
            // All controller requests share an IP budget, including requests to login.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor>() is null
                    ? RateLimitPartition.GetNoLimiter("non-api")
                    : CreatePartition(context, settings.ApiRequestsPerMinute));
            options.AddPolicy(ApiRateLimitOptions.LoginPolicy,
                context => CreatePartition(context, settings.LoginRequestsPerMinute));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                await response.WriteAsJsonAsync(new ApiErrorResponse(
                    (int)MessageCode.RateLimitExceeded, nameof(MessageCode.RateLimitExceeded),
                    MessageCatalog.GetMessage(MessageCode.RateLimitExceeded), context.HttpContext.TraceIdentifier), cancellationToken);
            };
        });
        return services;
    }

    // Use the connection address after trusted-proxy processing, never raw client-supplied headers.
    private static RateLimitPartition<string> CreatePartition(HttpContext context, int permitLimit) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.MapToIPv6().ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
}
