namespace bams.desktop.DTOs.Preferences;

/// <summary>A setting the signed-in user's role may edit, with the user's saved choice.</summary>
public sealed record PersonalPreferenceResponse(
    long SettingId,
    string UniqueCode,
    string Name,
    string Description,
    string ValueType,
    bool Enabled,
    string? Value,
    string UpdateKey);

/// <summary>Saves the "Default Vault Opening" preference. UpdateKey is the key from the last load or save.</summary>
public sealed record UpdateDefaultVaultOpeningRequest(bool Enabled, string? Value, string UpdateKey);
