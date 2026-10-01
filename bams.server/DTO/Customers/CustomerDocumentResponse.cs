using bams.server.Models.Customers;

namespace bams.server.DTO.Customers;

public sealed record CustomerDocumentResponse(
    long Id,
    long CustomerId,
    DocumentType DocumentType,
    string? DocumentNumber,
    string? FileReference,
    DateOnly? IssuedDate,
    DateOnly? ExpiryDate,
    DateTime? VerifiedAt,
    long? VerifiedBy);