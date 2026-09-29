using Microsoft.AspNetCore.Http;

namespace bams.server.Services.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string fileReference,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a stored file reference to its physical location on disk.
    /// </summary>
    string GetPhysicalPath(string fileReference);
}