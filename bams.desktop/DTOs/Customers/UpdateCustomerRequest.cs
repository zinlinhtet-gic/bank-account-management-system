namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's UpdateCustomerRequest (PATCH /api/customers/{id}, multipart form). Only
/// supplied (non-null) properties change; omitted properties keep their current value.
/// </summary>
public sealed record UpdateCustomerRequest(
    CustomerType? CustomerType,
    string? FullName,
    DateOnly? DateOfBirth,
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
    List<UpdateCustomerDocumentRequest>? Documents);
