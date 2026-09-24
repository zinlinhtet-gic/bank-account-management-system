namespace bams.desktop.Models.Configuration;

/// Represents one interest rate rule row for display on the Interest Rate list page.
public sealed class InterestRateRowModel
{
    public string AccountType { get; init; } = string.Empty;
    public string Term { get; init; } = string.Empty;
    public string AnnualRate { get; init; } = string.Empty;
    public string MinBalance { get; init; } = string.Empty;
    public string MaxBalance { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
