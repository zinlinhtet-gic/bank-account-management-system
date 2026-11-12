using Microsoft.AspNetCore.Http;

namespace bams.server.Services.Interfaces;

public interface IFileStorageService
{
    /// <summary>
    /// Validates and stores a file under <c>uploads/{folder}</c> using a generated GUID filename.
    /// </summary>
    Task<string> SaveAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates and stores an account document under <c>uploads/accounts/{accountId}</c>.
    /// </summary>
    Task<string> SaveAccountDocumentAsync(
        long accountId,
        IFormFile file,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates file length, extension, content type, and leading file signature.
    /// </summary>
    Task ValidateFileAsync(IFormFile file, CancellationToken cancellationToken);

    Task DeleteAsync(
        string fileReference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a stored file synchronously, for rollback cleanup that must not depend on a cancellable token.
    /// </summary>
    void DeleteFile(string fileReference);

    /// <summary>
    /// Resolves a stored file reference to its physical location on disk.
    /// </summary>
    string GetPhysicalPath(string fileReference);
}
