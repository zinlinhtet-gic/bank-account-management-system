using bams.server.Models.Customers;

namespace bams.server.Models.Accounts;

/// <summary>Links a customer who referred an account opening to the created account.</summary>
public sealed class AccountReferer
{
    public long AccountId { get; set; }

    public Account? Account { get; set; }

    public long CustomerId { get; set; }

    public Customer? Customer { get; set; }
}
