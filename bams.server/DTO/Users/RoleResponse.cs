namespace bams.server.DTO.Users;

/// <summary>
/// A role that can be assigned in User Management.
/// </summary>
/// <param name="Code">Stable role code, e.g. "manager".</param>
/// <param name="Name">Display name stored for the role.</param>
/// <param name="DefaultPassword">
/// The password a new or reset user of this role receives. Shown to managers so they can pass it on;
/// the user must change it at the next login.
/// </param>
public sealed record RoleResponse(
    string Code,
    string Name,
    string DefaultPassword);
