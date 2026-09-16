namespace bams.server.Models.Products;

public sealed class FeeRule
{
    public long Id { get; set; }

    public long AccountTypeId { get; set; }

    public AccountType? AccountType { get; set; }

    public FeeType FeeType { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Percentage { get; set; }

    public decimal? MinimumFee { get; set; }

    public decimal? MaximumFee { get; set; }

    public decimal? TaxRate { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public string Status { get; set; } = string.Empty;
}
