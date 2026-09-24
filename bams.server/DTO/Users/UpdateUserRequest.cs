namespace bams.server.DTO.Users;

/// <summary>
/// Full update of a staff user's profile and role. The password is changed only through reset-password.
/// </summary>
/// <param name="Role">Role code, e.g. "officer".</param>
public sealed record UpdateUserRequest(
    string FullName,
    string Username,
    string Email,
    string? Phone,
    string Role);
