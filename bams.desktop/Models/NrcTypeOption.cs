namespace bams.desktop.Models;

/// <summary>One entry of the NRC Type drop-down on the create-customer form: the letter shown in
/// parentheses on the NRC number itself, e.g. the "N" in "9/AHMAYA(N)123456". Fixed by the server's
/// NRC number format (see CustomerFieldRules.NrcNumberRegexPattern's "[NEP]" group), not configurable.</summary>
public sealed record NrcTypeOption(string Code, string DisplayName)
{
    public static IReadOnlyList<NrcTypeOption> All { get; } =
    [
        new("N", "N"),
        new("E", "E"),
        new("P", "P")
    ];

    // ComboBox shows ToString() when no template is set.
    public override string ToString() => DisplayName;
}
