using bams.desktop.DTOs.Preferences;

namespace bams.desktop.Services;

public interface IPersonalPreferenceService
{
    /// <summary>Gets the settings the signed-in user's role may edit. Empty means the screen is hidden.</summary>
    Task<IReadOnlyList<PersonalPreferenceResponse>> GetEditablePreferencesAsync(CancellationToken cancellationToken);

    /// <summary>Saves the "Default Vault Opening" preference and returns the stored result with its new update key.</summary>
    Task<PersonalPreferenceResponse> UpdateDefaultVaultOpeningAsync(
        UpdateDefaultVaultOpeningRequest request,
        CancellationToken cancellationToken);
}
