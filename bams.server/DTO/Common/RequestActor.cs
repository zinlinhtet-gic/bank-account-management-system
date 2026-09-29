namespace bams.server.DTO.Common;

/// <summary>
/// The signed-in user making a request, plus the client details recorded in the audit log.
/// Built by controllers with <c>HttpContext.GetRequestActor()</c> so services never touch <c>HttpContext</c>.
/// </summary>
public sealed record RequestActor(
    long UserId,
    string? IpAddress,
    string? DeviceInfo);
