using bams.server.Models.Customers;

namespace bams.server.Models.Products;

public sealed class AccountTypeRequiredDocument
{
    public long AccountTypeId { get; set; }

    public AccountType? AccountType { get; set; }

    public DocumentType DocumentType { get; set; }
}
