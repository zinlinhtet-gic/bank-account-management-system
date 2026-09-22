using bams.server.DTO.Customers;
using bams.server.Models.Customers;

namespace bams.server.Mapping;

public static class CustomerMappings
{
    // Converts a Customer entity into the API response contract.
    public static CustomerResponse ToResponse(this Customer customer)
    {
        return new CustomerResponse(
            customer.Id,
            customer.CustomerNo,
            customer.CustomerType,
            customer.FullName,
            customer.DateOfBirth,
            customer.Nationality,
            customer.NrcNumber,
            customer.PassportNumber,
            customer.Phone,
            customer.Occupation,
            customer.AddressLine1,
            customer.AddressLine2,
            customer.City,
            customer.State,
            customer.PostalCode,
            customer.Country,
            customer.Email,
            customer.RiskLevel,
            customer.KycStatus,
            customer.Status,
            customer.CreatedAt,
            customer.Documents.Select(d => d.ToResponse()).ToList()
        );
    }

    /// <summary>
    /// Converts a CustomerDocument entity into the API response contract.
    /// </summary>
    public static CustomerDocumentResponse ToResponse(
        this CustomerDocument document)
    {
        return new CustomerDocumentResponse(
            document.Id,
            document.CustomerId,
            document.DocumentType,
            document.DocumentNumber,
            document.FileReference,
            document.IssuedDate,
            document.ExpiryDate,
            document.VerifiedAt,
            document.VerifiedBy);
    }
}
