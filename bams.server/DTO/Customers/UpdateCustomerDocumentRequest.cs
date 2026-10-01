using bams.server.Models.Customers;

namespace bams.server.DTO.Customers;

/// <summary>
/// One document entry within an update-customer request. Supplying <see cref="Id"/> edits
/// that existing document (only non-null fields are applied); omitting it adds a new
/// document, which requires <see cref="DocumentType"/>. <see cref="File"/> is optional in
/// both cases: when present, it is saved and becomes (or replaces) the document's file.
/// </summary>
public sealed record UpdateCustomerDocumentRequest(
    long? Id,
    DocumentType? DocumentType,
    string? DocumentNumber,
    IFormFile? File,
    DateOnly? IssuedDate,
    DateOnly? ExpiryDate);
