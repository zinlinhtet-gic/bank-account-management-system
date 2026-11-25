namespace bams.server.DTO.Preferences;

/// <summary>
/// One setting the caller's role may edit, with the caller's current choice.
/// <paramref name="UpdateKey"/> must be sent back unchanged when saving.
/// </summary>
public sealed record PersonalPreferenceResponse(
    long SettingId,
    string UniqueCode,
    string Name,
    string Description,
    string ValueType,
    bool Enabled,
    string? Value,
    string UpdateKey);

/// <summary>
/// Saves the "Default Vault Opening" preference. <paramref name="Value"/> is the amount as text, e.g. "1500000.00".
/// </summary>
public sealed record UpdateDefaultVaultOpeningRequest(
    bool Enabled,
    string? Value,
    string? UpdateKey);
