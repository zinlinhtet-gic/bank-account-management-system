using bams.server.Models.Customers;

namespace bams.server.DTO.Customers;

/// <summary>
/// Partial-update contract for PATCH /api/customers/{id}. Only supplied (non-null)
/// properties are applied; omitted or null properties leave the existing value unchanged.
/// <see cref="Documents"/> entries with an <see cref="UpdateCustomerDocumentRequest.Id"/>
/// edit an existing document; entries without one add a new document.
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
