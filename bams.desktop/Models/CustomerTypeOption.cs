using bams.desktop.DTOs.Customers;

namespace bams.desktop.Models;

/// <summary>One entry of the Customer Type drop-down on the create-customer form.</summary>
public sealed record CustomerTypeOption(CustomerType Value, string DisplayName)
{
    public static IReadOnlyList<CustomerTypeOption> All { get; } =
    [
        new(CustomerType.Citizen, "Citizen"),
        new(CustomerType.Foreigner, "Foreigner")
    ];

    // ComboBox shows ToString() when no template is set.
    public override string ToString() => DisplayName;
}
