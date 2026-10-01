namespace bams.desktop.DTOs.Customers;

/// <summary>
/// One document attached to a create-customer request. <see cref="FilePath"/> is a local file path;
/// the service streams it as the server's <c>IFormFile</c> when building the multipart request.
/// </summary>
public sealed record CreateCustomerDocumentRequest(
    DocumentType DocumentType,
    string? DocumentNumber,
    string? FilePath,
    DateOnly? IssuedDate,
    DateOnly? ExpiryDate);
