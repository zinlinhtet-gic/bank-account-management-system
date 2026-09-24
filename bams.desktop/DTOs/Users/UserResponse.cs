namespace bams.desktop.DTOs.Users;

/// <summary>
/// Full staff user detail (server: <c>DTO/Users/UserResponse</c>). Timestamps are UTC.
/// </summary>
/// <param name="Role">Role code, e.g. "manager".</param>
/// <param name="IsOnline">Signed in and active within the last few minutes (decided by the server).</param>
/// <param name="LastSeenAt">Last login or heartbeat (UTC); null if the user never signed in.</param>
public sealed record UserResponse(
    long Id,
    string Username,
    string FullName,
    string Email,
    string? Phone,
    string Role,
    UserStatus Status,
    DateTime? LastLoginAt,
    bool MustChangePassword,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsOnline,
    DateTime? LastSeenAt);
