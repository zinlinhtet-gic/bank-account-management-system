namespace bams.server.Constants;

/// <summary>
/// Personal preference and setting invariants shared by configuration, validation and the migration seed.
/// </summary>
public static class PreferenceConstants
{
    // Column lengths (also used by Data/Configurations/PreferenceConfigurations.cs).
    public const int SettingNameMaximumLength = 150;
    public const int SettingCodeMaximumLength = 64;
    public const int SettingDescriptionMaximumLength = 500;
    public const int SettingValueTypeMaximumLength = 32;
    public const int PreferenceValueMaximumLength = 256;
    public const int UpdateKeyMaximumLength = 36;

    // Value types a setting can declare; the server validates value_set against the type.
    public const string DecimalValueType = "Decimal";

    // The "Default Vault Opening" setting: the manager's preferred starting vault cash for a business date.
    public const string DefaultVaultOpeningCode = "default_vault_opening";
    public const string DefaultVaultOpeningName = "Default Vault Opening";
    public const string DefaultVaultOpeningDescription = "Default starting vault cash used when opening a business date.";

    // Money values carry two decimals, the same rule as the business-date starting cash.
    public const int MoneyDecimalPlaces = 2;
}
