using bams.server.Models.Security;

namespace bams.server.Models.Preferences;

/// <summary>
/// One user's choice for one setting. Exactly one row exists per (user, setting).
/// </summary>
public sealed class PersonalPreference
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public User? User { get; set; }

    public long SettingId { get; set; }

    public Setting? Setting { get; set; }

    public bool Enabled { get; set; }

    // Stored as text; Setting.ValueType decides how it is parsed. Null when no value is set.
    public string? ValueSet { get; set; }

    // Concurrency token: regenerated on every save, so a stale client update is rejected.
    public string UpdateKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
