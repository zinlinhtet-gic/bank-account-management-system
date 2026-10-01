namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's CustomerDocumentResponse (one document attached to a customer).
/// </summary>
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
