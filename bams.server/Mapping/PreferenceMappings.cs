using bams.server.DTO.Preferences;
using bams.server.Models.Preferences;

namespace bams.server.Mapping;

public static class PreferenceMappings
{
    // Converts a PersonalPreference (with Setting loaded) into the API response contract.
    public static PersonalPreferenceResponse ToResponse(this PersonalPreference preference)
    {
        Setting setting = preference.Setting!;

        return new PersonalPreferenceResponse(
            setting.Id,
            setting.UniqueCode,
            setting.Name,
            setting.Description,
            setting.ValueType,
            preference.Enabled,
            preference.ValueSet,
            preference.UpdateKey);
    }
}
