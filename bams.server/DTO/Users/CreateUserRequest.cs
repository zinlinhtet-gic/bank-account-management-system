namespace bams.server.DTO.Users;

/// <summary>
/// Input for creating a staff user. The password is not sent: the role's default password is used
/// and the user must change it at the first login.
/// </summary>
/// <param name="Role">Role code, e.g. "officer".</param>
public sealed record CreateUserRequest(
    string FullName,
    string Username,
    string Email,
    string? Phone,
    string Role);
