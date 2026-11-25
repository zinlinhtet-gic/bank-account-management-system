using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Preferences;

namespace bams.desktop.Services;

public sealed class PersonalPreferenceService : IPersonalPreferenceService
{
    private readonly ApiClient _apiClient;

    public PersonalPreferenceService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<PersonalPreferenceResponse>> GetEditablePreferencesAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<PersonalPreferenceResponse>>(
            ApiConstants.PersonalPreferencesEndpoint,
            cancellationToken);
    }

    public Task<PersonalPreferenceResponse> UpdateDefaultVaultOpeningAsync(
        UpdateDefaultVaultOpeningRequest request,
        CancellationToken cancellationToken)
    {
        return _apiClient.PutAsync<UpdateDefaultVaultOpeningRequest, PersonalPreferenceResponse>(
            ApiConstants.DefaultVaultOpeningEndpoint,
            request,
            cancellationToken);
    }
}
