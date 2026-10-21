using bams.server.DTO.Accounts;
using bams.server.Models.Accounts;

namespace bams.server.Services.Interfaces;

public interface IAccountDocumentService
{
    /// <summary>
    /// Validates uploads and holder-owned source references against the account type's document requirements.
    /// </summary>
    Task ValidateRequiredDocumentsAsync(
        long accountTypeId,
        IReadOnlyList<AccountDocumentUploadRequest> documents,
        IReadOnlyList<long> holderIds,
        CancellationToken cancellationToken);

    /// <summary>Selects one eligible stored document per type, preferring the first holder.</summary>
    Task<IReadOnlyList<ReusableCustomerDocumentResponse>> GetReusableDocumentsAsync(
        IReadOnlyList<long> holderIds, CancellationToken cancellationToken);

    /// <summary>Gets document requirements for the selected account products.</summary>
    Task<IReadOnlyList<AccountTypeRequiredDocumentResponse>> GetRequiredDocumentsAsync(
        IReadOnlyCollection<long> accountTypeIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stores uploads or independent customer-file copies and tracks their account-document metadata.
    /// </summary>
    Task<IReadOnlyList<string>> StoreAccountDocumentsAsync(
        Account account,
        IReadOnlyList<AccountDocumentUploadRequest> documents,
        IReadOnlyList<long> holderIds,
        DateTime uploadedAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes stored files when account creation cannot be committed.
    /// </summary>
    void DeleteStoredFiles(IEnumerable<string> fileReferences);
}
