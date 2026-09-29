using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Models.Configuration;

/// <summary>
/// A fee type in the fee rule form's drop-down.
/// </summary>
public sealed record FeeTypeOption(FeeType Value, string DisplayName)
{
    public static readonly IReadOnlyList<FeeTypeOption> All =
    [
        new(FeeType.Maintenance, "Maintenance"),
        new(FeeType.Transfer, "Transfer"),
        new(FeeType.InterbankTransfer, "Interbank transfer"),
        new(FeeType.EarlyWithdrawal, "Early withdrawal"),
        new(FeeType.DormantAccount, "Dormant account"),
        new(FeeType.CashTransfer, "Cash transfer")
    ];

    // ComboBox shows ToString() when no template is set.
    public override string ToString() => DisplayName;
}
