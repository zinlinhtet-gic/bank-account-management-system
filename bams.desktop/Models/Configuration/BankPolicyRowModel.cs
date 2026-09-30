using bams.desktop.DTOs.Configuration;

namespace bams.desktop.Models.Configuration;

/// Represents one account type's policy row for display on the Bank Policies list page.
public sealed class BankPolicyRowModel
{
    public string AccountType { get; init; } = string.Empty;
    public string MinOpeningBalance { get; init; } = string.Empty;
    public string MinMaintainedBalance { get; init; } = string.Empty;
    public string DailyLimit { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>The raw record behind this row, passed to the edit form as-is.</summary>
    public required BankPolicyResponse Source { get; init; }
}
