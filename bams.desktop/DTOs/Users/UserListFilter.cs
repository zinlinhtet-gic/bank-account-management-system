namespace bams.desktop.DTOs.Users;

/// <summary>
/// Filters sent as the query string of <c>GET api/users</c>. Null or empty values are left out.
/// </summary>
/// <param name="Search">Part of the username, full name or email.</param>
/// <param name="Role">Role code; null for all roles.</param>
/// <param name="CreatedFrom">Users created at or after this instant.</param>
/// <param name="CreatedBefore">Users created before this instant (exclusive).</param>
public sealed record UserListFilter(
    string? Search,
    string? Role,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedBefore);
