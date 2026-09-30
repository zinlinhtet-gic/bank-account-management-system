using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Models.Configuration;

/// <summary>
/// An account type category in the bank policy form's drop-down.
/// </summary>
public sealed record AccountTypeCategoryOption(AccountTypeCategory Value, string DisplayName)
{
    public static readonly IReadOnlyList<AccountTypeCategoryOption> All =
    [
        new(AccountTypeCategory.CURRENT, "Current"),
        new(AccountTypeCategory.SAVING, "Saving"),
        new(AccountTypeCategory.FIXED, "Fixed"),
        new(AccountTypeCategory.CALL, "Call")
    ];

    // ComboBox shows ToString() when no template is set.
    public override string ToString() => DisplayName;
}
