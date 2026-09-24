using bams.server.Models.Security;

namespace bams.server.DTO.Users;

/// <summary>
/// Full staff user detail, returned by get-by-id, create, update and reset-password.
/// </summary>
/// <param name="Role">Role code, e.g. "manager".</param>
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
    DateTime UpdatedAt);
