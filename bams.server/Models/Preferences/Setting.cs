namespace bams.server.Models.Preferences;

/// <summary>
/// Catalog entry for a personal setting. The *Editable flags say which roles may change it.
/// </summary>
public sealed class Setting
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    // Stable code used by code and clients; never renamed once released.
    public string UniqueCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    // Tells the server how to validate PersonalPreference.ValueSet (see PreferenceConstants).
    public string ValueType { get; set; } = string.Empty;

    public bool ManagerEditable { get; set; }

    public bool AuditorEditable { get; set; }

    public bool OfficerEditable { get; set; }
}
