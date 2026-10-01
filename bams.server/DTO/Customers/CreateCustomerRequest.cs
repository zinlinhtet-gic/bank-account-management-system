using bams.server.Models.Customers;

namespace bams.server.DTO.Customers;

public sealed record CreateCustomerRequest(
    CustomerType CustomerType,
    string FullName,
    DateOnly DateOfBirth,
    string? Nationality,
    string? NrcNumber,
    string? PassportNumber,
    string? Phone,
    string? Occupation,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Email,
     List<CreateCustomerDocumentRequest>? Documents
    );

