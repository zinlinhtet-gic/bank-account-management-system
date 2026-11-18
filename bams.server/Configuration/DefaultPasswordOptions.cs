using bams.server.Constants;

namespace bams.server.Configuration;

/// <summary>Configured temporary passwords shared by seeding, user creation and resets.</summary>
public sealed class DefaultPasswordOptions
{
    public const string SectionName = "DefaultPasswords";

    public Dictionary<string, string> ByRole { get; set; } = new();

    // Require all supported roles before startup can modify security data.
    public bool HasRequiredPasswords() =>
        new[] { SecurityConstants.ManagerRole, SecurityConstants.OfficerRole, SecurityConstants.AuditorRole }
            .All(role => ByRole.TryGetValue(role, out var password) && !string.IsNullOrWhiteSpace(password));
}
