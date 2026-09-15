namespace bams.desktop.Models;

/// <summary>
/// Represents account data in a shape that is convenient for WPF binding.
/// </summary>
public sealed class ExampleAccountDisplayModel
{
    public long Id { get; init; }

    public string AccountNumber { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public decimal Balance { get; init; }

    public string FormattedBalance => Balance.ToString("C");
}
