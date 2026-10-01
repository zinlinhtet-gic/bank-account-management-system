using bams.server.Models.Customers;

namespace bams.server.DTO.Customers;

public sealed record CreateCustomerDocumentRequest(
    DocumentType DocumentType,
    string? DocumentNumber,
    IFormFile? File,
    DateOnly? IssuedDate,
    DateOnly? ExpiryDate);
