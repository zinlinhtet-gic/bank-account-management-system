using bams.desktop.Constants;
using bams.desktop.DTOs.Users;

namespace bams.desktop.Models;

/// <summary>
/// A role in a drop-down (the user form's role picker and the list's role filter).
/// </summary>
/// <param name="Code">Role code sent to the server; null for the "All roles" filter entry.</param>
/// <param name="DisplayName">Label shown in the drop-down.</param>
/// <param name="DefaultPassword">Password a new or reset user of this role receives; empty for "All roles".</param>
public sealed record RoleOption(string? Code, string DisplayName, string DefaultPassword)
{
    /// <summary>
    /// The "no role filter" entry at the top of the role filter.
    /// </summary>
    public static RoleOption AllRoles { get; } = new(null, "All roles", string.Empty);

    /// <summary>
    /// Builds an option from a server role.
    /// </summary>
    public static RoleOption FromResponse(RoleResponse role)
    {
        return new RoleOption(role.Code, RoleCodes.ToDisplayName(role.Code), role.DefaultPassword);
    }

    // ComboBox shows ToString() when no template is set.
    public override string ToString() => DisplayName;
}
