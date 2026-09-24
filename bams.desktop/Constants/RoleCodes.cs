namespace bams.desktop.Constants;

/// <summary>
/// Role codes returned by the server. Values must match <c>bams.server/Constants/SecurityConstants.cs</c>.
/// Use them for display and filtering only; access decisions use <see cref="PermissionCodes"/>.
/// </summary>
public static class RoleCodes
{
    public const string Manager = "manager";
    public const string Officer = "officer";
    public const string Auditor = "auditor";

    /// <summary>
    /// Turns a role code into a label for the screen, e.g. "manager" → "Manager".
    /// </summary>
    public static string ToDisplayName(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return string.Empty;
        }

        return char.ToUpperInvariant(roleCode[0]) + roleCode[1..].ToLowerInvariant();
    }
}
