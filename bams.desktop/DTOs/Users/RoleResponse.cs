namespace bams.desktop.DTOs.Users;

/// <summary>
/// A role that can be assigned in User Management (server: <c>DTO/Users/RoleResponse</c>).
/// </summary>
/// <param name="DefaultPassword">Password given to a new or reset user of this role.</param>
public sealed record RoleResponse(
    string Code,
    string Name,
    string DefaultPassword);
