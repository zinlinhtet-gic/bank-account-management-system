namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's UpdateCustomerDocumentRequest. Supplying <see cref="Id"/> edits that
/// existing document; omitting it adds a new one (which needs <see cref="DocumentType"/>).
/// <see cref="FilePath"/> is optional in both cases: when present, it replaces (or becomes) the
/// document's file.
/// </summary>
public sealed record UpdateCustomerDocumentRequest(
    long? Id,
    DocumentType? DocumentType,
    string? DocumentNumber,
    string? FilePath,
    DateOnly? IssuedDate,
    DateOnly? ExpiryDate);
