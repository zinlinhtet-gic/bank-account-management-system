namespace bams.server.DTO.Auth;

/// <summary>
/// Response containing user permissions and role information.
/// </summary>
public sealed record PermissionsResponse(
    long UserId,
    string Username,
    string Role,
    IReadOnlyList<string> Permissions);