namespace bams.desktop.Models.Configuration;

/// Represents one account type's policy row for display on the Bank Policies list page.
public sealed class BankPolicyRowModel
{
    public string AccountType { get; init; } = string.Empty;
    public string MinOpeningBalance { get; init; } = string.Empty;
    public string MinMaintainedBalance { get; init; } = string.Empty;
    public string DailyLimit { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
