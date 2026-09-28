namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's CreateCustomerRequest (POST /api/customers, multipart form). Every field
/// besides CustomerType, FullName and DateOfBirth is optional, matching the server contract.
/// </summary>
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
    List<CreateCustomerDocumentRequest>? Documents);
