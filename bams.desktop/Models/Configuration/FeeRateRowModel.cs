namespace bams.desktop.Models.Configuration;

/// Represents one fee rule row for display on the Fee Rate list page.
public sealed class FeeRateRowModel
{
    public string AccountType { get; init; } = string.Empty;
    public string FeeType { get; init; } = string.Empty;
    public string Amount { get; init; } = string.Empty;
    public string Percentage { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
