using bams.server.DTO.Common;
using bams.server.DTO.Preferences;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace bams.server.Controllers;

/// <summary>
/// The signed-in user's personal preferences. Any authenticated user may call these; what a role may edit
/// is decided by the Settings table, not by a permission.
/// </summary>
[ApiController]
[Route("api/personal-preferences")]
public sealed class PersonalPreferencesController : ControllerBase
{
    private readonly IPersonalPreferenceService _personalPreferenceService;

    public PersonalPreferencesController(IPersonalPreferenceService personalPreferenceService)
    {
        _personalPreferenceService = personalPreferenceService;
    }

    /// <summary>
    /// Lists the settings the caller's role may edit. An empty list means the screen should be hidden.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiMessageResponse<IReadOnlyList<PersonalPreferenceResponse>>>> GetEditablePreferencesAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PersonalPreferenceResponse> preferences =
            await _personalPreferenceService.GetEditablePreferencesAsync(cancellationToken);

        return Ok(ApiMessageResponse<IReadOnlyList<PersonalPreferenceResponse>>.FromCode(MessageCode.Success, preferences));
    }

    /// <summary>
    /// Saves the caller's "Default Vault Opening" enabled flag and value.
    /// </summary>
    [HttpPut("default-vault-opening")]
    public async Task<ActionResult<ApiMessageResponse<PersonalPreferenceResponse>>> UpdateDefaultVaultOpeningAsync(
        UpdateDefaultVaultOpeningRequest request,
        CancellationToken cancellationToken)
    {
        PersonalPreferenceResponse preference =
            await _personalPreferenceService.UpdateDefaultVaultOpeningAsync(request, cancellationToken);

        return Ok(ApiMessageResponse<PersonalPreferenceResponse>.FromCode(
            MessageCode.PersonalPreferenceUpdatedSuccessfully,
            preference));
    }
}
