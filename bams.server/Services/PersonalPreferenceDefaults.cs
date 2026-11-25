using bams.server.Models.Preferences;

namespace bams.server.Services;

/// <summary>
/// Builds the starting preference row for a setting. Shared by user creation and the startup backfill
/// so the default shape (value unset, fresh update key) is defined once.
/// </summary>
public static class PersonalPreferenceDefaults
{
    // Creates a preference row with no value and a new update key. The caller attaches the owner
    // (User or UserId) because a user being created has no Id yet, and decides whether it starts enabled.
    public static PersonalPreference CreateFor(long settingId, bool enabled, DateTime nowUtc)
    {
        return new PersonalPreference
        {
            SettingId = settingId,
            Enabled = enabled,
            ValueSet = null,
            UpdateKey = Guid.NewGuid().ToString(),
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc
        };
    }
}
