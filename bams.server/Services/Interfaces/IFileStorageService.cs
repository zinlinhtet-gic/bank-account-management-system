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
}