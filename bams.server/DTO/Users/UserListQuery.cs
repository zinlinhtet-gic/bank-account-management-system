namespace bams.server.DTO.Users;

/// <summary>
/// Optional filters for <c>GET api/users</c>, all read from the query string. Deleted users are never listed.
/// </summary>
/// <param name="Search">Matches part of the username, full name or email.</param>
/// <param name="Role">Role code, e.g. "manager". Empty means all roles.</param>
/// <param name="CreatedFrom">Only users created at or after this instant.</param>
/// <param name="CreatedBefore">Only users created before this instant (exclusive), e.g. the start of the day after the last day wanted.</param>
public sealed record UserListQuery(
    string? Search,
    string? Role,
    DateTimeOffset? CreatedFrom,
    DateTimeOffset? CreatedBefore);
