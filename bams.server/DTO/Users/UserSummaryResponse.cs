namespace bams.server.DTO.Users;

/// <summary>
/// One row of the User Management list.
/// </summary>
/// <param name="Role">Role code, e.g. "manager".</param>
public sealed record UserSummaryResponse(
    long Id,
    string Username,
    string FullName,
    string Email,
    string Role,
    DateTime CreatedAt);
