using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;

namespace bams.server.Services.Interfaces;

public interface IAccountDocumentService
{
    /// <summary>
    /// Validates that uploads satisfy the account type's document requirements.
    /// </summary>
    Task ValidateRequiredDocumentsAsync(
        long accountTypeId,
        IReadOnlyList<AccountDocumentUploadRequest> documents,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores uploaded files and tracks their account-document metadata.
    /// </summary>
    Task<IReadOnlyList<string>> StoreAccountDocumentsAsync(
        Account account,
        IReadOnlyList<AccountDocumentUploadRequest> documents,
        DateTime uploadedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes stored files when account creation cannot be committed.
    /// </summary>
    void DeleteStoredFiles(IEnumerable<string> fileReferences);
}
