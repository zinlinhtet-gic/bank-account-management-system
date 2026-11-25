using bams.server.DTO.Preferences;

namespace bams.server.Services.Interfaces;

public interface IPersonalPreferenceService
{
    /// <summary>
    /// Lists the settings the current user's role may edit, with the user's current choices.
    /// </summary>
    Task<IReadOnlyList<PersonalPreferenceResponse>> GetEditablePreferencesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the current user's "Default Vault Opening" enabled flag and value.
    /// </summary>
    Task<PersonalPreferenceResponse> UpdateDefaultVaultOpeningAsync(
        UpdateDefaultVaultOpeningRequest request,
        CancellationToken cancellationToken);
}
