namespace bams.desktop.DTOs.Users;

/// <summary>
/// Mirrors the server's <c>UserStatus</c>. Member names must match: enums travel as names in JSON.
/// </summary>
public enum UserStatus
{
    Active = 1,
    Disabled = 2,
    Deleted = 3
}
