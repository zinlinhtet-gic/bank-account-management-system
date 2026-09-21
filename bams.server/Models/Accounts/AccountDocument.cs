using bams.server.Models.Customers;

namespace bams.server.Models.Accounts;

public sealed class AccountDocument
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public Account? Account { get; set; }
    public DocumentType DocumentType { get; set; }
    public string? DocumentNumber { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileReference { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}
