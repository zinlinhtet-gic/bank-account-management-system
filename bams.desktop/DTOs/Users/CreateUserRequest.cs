namespace bams.desktop.DTOs.Users;

/// <summary>
/// Input for creating a staff user (server: <c>DTO/Users/CreateUserRequest</c>).
/// </summary>
/// <param name="Role">Role code, e.g. "officer".</param>
public sealed record CreateUserRequest(
    string FullName,
    string Username,
    string Email,
    string? Phone,
    string Role);
