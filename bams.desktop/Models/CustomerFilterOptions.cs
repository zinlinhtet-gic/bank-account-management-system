using bams.desktop.DTOs.Customers;

namespace bams.desktop.Models;

/// <summary>One entry of the KYC Status filter drop-down; a null <see cref="Value"/> selects "All".</summary>
public sealed record KycStatusFilterOption(KycStatus? Value, string DisplayName)
{
    public static IReadOnlyList<KycStatusFilterOption> All { get; } =
    [
        new(null, "All"),
        new(KycStatus.Pending, "Pending"),
        new(KycStatus.Verified, "Verified"),
        new(KycStatus.Rejected, "Rejected")
    ];

    // ComboBox shows ToString() when no template is set.
    public override string ToString() => DisplayName;
}

/// <summary>One entry of the Risk Level filter drop-down; a null <see cref="Value"/> selects "All".</summary>
public sealed record RiskLevelFilterOption(RiskLevel? Value, string DisplayName)
{
    public static IReadOnlyList<RiskLevelFilterOption> All { get; } =
    [
        new(null, "All"),
        new(RiskLevel.High, "High"),
        new(RiskLevel.Medium, "Medium"),
        new(RiskLevel.Low, "Low")
    ];

    public override string ToString() => DisplayName;
}

/// <summary>
/// One entry of the Status filter drop-down; a null <see cref="Value"/> selects "All". Matches the
/// server's raw Status string ("Active"/"Inactive") rather than an enum, since the server stores it
/// as a plain string.
/// </summary>
public sealed record CustomerStatusFilterOption(string? Value, string DisplayName)
{
    public static IReadOnlyList<CustomerStatusFilterOption> All { get; } =
    [
        new(null, "All"),
        new("Active", "Active"),
        new("Inactive", "Inactive")
    ];

    public override string ToString() => DisplayName;
}
