using bams.server.Constants;
using bams.server.DTO.Common;

namespace bams.server.Utils.Extensions;

public static class HttpContextExtensions
{
    /// <summary>
    /// Builds the <see cref="RequestActor"/> for the signed-in user: id, client IP and User-Agent, cut to the
    /// audit-log column sizes.
    /// </summary>
    public static RequestActor GetRequestActor(this HttpContext httpContext)
    {
        var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        var deviceInfo = httpContext.Request.Headers.UserAgent.ToString();

        return new RequestActor(
            httpContext.User.GetRequiredUserId(),
            Truncate(ipAddress, AuditConstants.IpAddressMaximumLength),
            Truncate(deviceInfo, AuditConstants.DeviceInfoMaximumLength));
    }

    // Returns null for empty values and cuts longer values to the maximum length.
    private static string? Truncate(string? value, int maximumLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return value.Length <= maximumLength ? value : value[..maximumLength];
    }
}
