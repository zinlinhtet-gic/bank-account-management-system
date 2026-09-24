using bams.server.Models.Customers;
using bams.server.Models.Accounts.Enums;

namespace bams.server.Models.Accounts;

public sealed class AccountHolder : IConcurrencyTracked
{
    public long Id { get; set; }

    public long AccountId { get; set; }

    public Account? Account { get; set; }

    public long CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public OwnershipType OwnershipType { get; set; }

    public decimal? OwnershipPercentage { get; set; }

    public bool IsPrimary { get; set; }

    public string? SigningRule { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public long Version { get; set; } = 1;
}
