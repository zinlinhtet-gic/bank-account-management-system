using bams.server.Models.Customers;

namespace bams.server.DTO.Accounts;

public sealed class AccountDocumentUploadRequest
{
    public DocumentType DocumentType { get; init; }
    public string? DocumentNumber { get; init; }
    public IFormFile File { get; init; } = null!;
}
