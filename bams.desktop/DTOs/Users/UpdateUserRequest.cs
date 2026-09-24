namespace bams.desktop.DTOs.Users;

/// <summary>
/// Full update of a staff user (server: <c>DTO/Users/UpdateUserRequest</c>).
/// </summary>
/// <param name="Role">Role code, e.g. "officer".</param>
public sealed record UpdateUserRequest(
    string FullName,
    string Username,
    string Email,
    string? Phone,
    string Role);
