using System.Globalization;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Preferences;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Preferences;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>
/// Personal preferences of the signed-in user. A setting is only listed or changeable when the user's role
/// has the matching *Editable flag on the Setting row.
/// </summary>
public sealed class PersonalPreferenceService : IPersonalPreferenceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public PersonalPreferenceService(ApplicationDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Lists the settings the current user's role may edit, with the user's current choices.
    /// </summary>
    public async Task<IReadOnlyList<PersonalPreferenceResponse>> GetEditablePreferencesAsync(
        CancellationToken cancellationToken)
    {
        long userId = _currentUserService.GetCurrentUserId();
        string roleCode = await GetRoleCodeAsync(userId, cancellationToken);

        List<PersonalPreference> preferences = await _dbContext.PersonalPreferences
            .AsNoTracking()
            .Include(preference => preference.Setting)
            .Where(preference => preference.UserId == userId)
            .OrderBy(preference => preference.SettingId)
            .ToListAsync(cancellationToken);

        // Filter in memory: the editable flag is a column per role, so it is picked by role code here.
        return preferences
            .Where(preference => IsSettingEditableForRole(preference.Setting!, roleCode))
            .Select(preference => preference.ToResponse())
            .ToList();
    }

    /// <summary>
    /// Saves the current user's "Default Vault Opening" enabled flag and value.
    /// </summary>
    public async Task<PersonalPreferenceResponse> UpdateDefaultVaultOpeningAsync(
        UpdateDefaultVaultOpeningRequest request,
        CancellationToken cancellationToken)
    {
        long userId = _currentUserService.GetCurrentUserId();
        string roleCode = await GetRoleCodeAsync(userId, cancellationToken);

        PersonalPreference preference = await GetTrackedPreferenceAsync(
            userId,
            PreferenceConstants.DefaultVaultOpeningCode,
            cancellationToken);

        EnsureSettingIsEditableForRole(preference.Setting!, roleCode);
        EnsureUpdateKeyMatches(preference, request.UpdateKey);
        string? normalizedValue = NormalizeAndValidateDefaultVaultOpeningValue(request);

        ApplyPreferenceChange(preference, request.Enabled, normalizedValue);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request changed the row between the key check and the save.
            throw new ConflictException(MessageCode.ConcurrentModification);
        }

        return preference.ToResponse();
    }

    // Returns the user's role code (a user has one role in practice), or empty when none is assigned.
    private async Task<string> GetRoleCodeAsync(long userId, CancellationToken cancellationToken)
    {
        string? roleCode = await _dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == userId)
            .Select(userRole => userRole.Role!.Code)
            .FirstOrDefaultAsync(cancellationToken);

        return roleCode ?? string.Empty;
    }

    // Loads the user's tracked preference row for a setting code, or throws PersonalPreferenceNotFound.
    private async Task<PersonalPreference> GetTrackedPreferenceAsync(
        long userId,
        string settingCode,
        CancellationToken cancellationToken)
    {
        PersonalPreference? preference = await _dbContext.PersonalPreferences
            .Include(item => item.Setting)
            .FirstOrDefaultAsync(
                item => item.UserId == userId && item.Setting!.UniqueCode == settingCode,
                cancellationToken);

        return preference ?? throw new NotFoundException(MessageCode.PersonalPreferenceNotFound);
    }

    // True when the setting's editable flag for this role is set. Unknown roles can edit nothing.
    private static bool IsSettingEditableForRole(Setting setting, string roleCode)
    {
        return roleCode switch
        {
            SecurityConstants.ManagerRole => setting.ManagerEditable,
            SecurityConstants.AuditorRole => setting.AuditorEditable,
            SecurityConstants.OfficerRole => setting.OfficerEditable,
            _ => false
        };
    }

    // Rejects a change to a setting the user's role is not allowed to edit.
    private static void EnsureSettingIsEditableForRole(Setting setting, string roleCode)
    {
        if (!IsSettingEditableForRole(setting, roleCode))
        {
            throw new ForbiddenException(MessageCode.InsufficientPermission);
        }
    }

    // Rejects a save made from an outdated snapshot (the row changed since the client loaded it).
    private static void EnsureUpdateKeyMatches(PersonalPreference preference, string? requestUpdateKey)
    {
        if (string.IsNullOrWhiteSpace(requestUpdateKey))
        {
            throw new ValidationException(MessageCode.RequiredFieldMissing);
        }

        if (!string.Equals(preference.UpdateKey, requestUpdateKey, StringComparison.Ordinal))
        {
            throw new ConflictException(MessageCode.ConcurrentModification);
        }
    }

    // Returns the value as stored text. When enabled the value must be a positive two-decimal amount
    // (same rule as scheduled starting cash); when disabled an empty value clears it and any value must still be valid.
    private static string? NormalizeAndValidateDefaultVaultOpeningValue(UpdateDefaultVaultOpeningRequest request)
    {
        string? text = string.IsNullOrWhiteSpace(request.Value) ? null : request.Value.Trim();

        if (text is null)
        {
            if (request.Enabled)
            {
                throw new ValidationException(MessageCode.RequiredFieldMissing);
            }

            return null;
        }

        bool isNumber = decimal.TryParse(
            text,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out decimal amount);

        if (!isNumber
            || amount <= 0m
            || decimal.Round(amount, PreferenceConstants.MoneyDecimalPlaces) != amount
            || text.Length > PreferenceConstants.PreferenceValueMaximumLength)
        {
            throw new ValidationException(MessageCode.InvalidAmount);
        }

        // Store the canonical form so the same amount is always saved the same way.
        return amount.ToString("F" + PreferenceConstants.MoneyDecimalPlaces, CultureInfo.InvariantCulture);
    }

    // Applies the new values and issues a new update key so older snapshots can no longer save.
    private static void ApplyPreferenceChange(PersonalPreference preference, bool enabled, string? value)
    {
        preference.Enabled = enabled;
        preference.ValueSet = value;
        preference.UpdateKey = Guid.NewGuid().ToString();
        preference.UpdatedAt = DateTime.UtcNow;
    }
}
