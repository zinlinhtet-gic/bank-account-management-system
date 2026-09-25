namespace bams.server.Models.Customers;

public sealed class Customer
{
    public long Id { get; set; }

    public string CustomerNo { get; set; } = string.Empty;

    public CustomerType CustomerType { get; set; }


    public string FullName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public string? Nationality { get; set; }

    public string? NrcNumber { get; set; }

    public string? PassportNumber { get; set; }

    public string? Phone { get; set; }

    public string? Occupation { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string? Country { get; set; }

    public string? Email { get; set; }

    public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;

    public KycStatus KycStatus { get; set; } = KycStatus.Pending;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
