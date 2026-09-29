namespace bams.desktop.DTOs.Accounts;

public sealed record AccountDocumentUpload(
    string DocumentType,
    string? DocumentNumber,
    string FilePath);
