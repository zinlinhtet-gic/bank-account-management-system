namespace bams.desktop.DTOs.Auth;

/// <summary>
/// Response containing user permissions and role information from the server.
/// </summary>
public sealed record PermissionsResponse(
    long UserId,
    string Username,
    string Role,
    IReadOnlyList<string> Permissions);