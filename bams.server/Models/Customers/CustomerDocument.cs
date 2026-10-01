using bams.server.Models.Security;

namespace bams.server.Models.Customers;

public sealed class CustomerDocument
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public DocumentType DocumentType { get; set; }

    public string? DocumentNumber { get; set; }

    public string? FileReference { get; set; }

    public DateOnly? IssuedDate { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public long? VerifiedBy { get; set; }

    public User? VerifiedByUser { get; set; }
}
