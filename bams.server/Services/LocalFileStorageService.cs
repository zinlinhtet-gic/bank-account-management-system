using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace bams.server.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;

    public LocalFileStorageService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new ValidationException(MessageCode.CustomerDocumentFileEmpty);
        }

        var extension = Path.GetExtension(file.FileName);

        var fileName = $"{Guid.NewGuid():N}{extension}";

        var relativeFolder = Path.Combine(
            "uploads",
            folder);

        var physicalFolder = Path.Combine(
            _environment.ContentRootPath,
            relativeFolder);

        Directory.CreateDirectory(physicalFolder);

        var physicalPath = Path.Combine(
            physicalFolder,
            fileName);

        await using var stream = new FileStream(
            physicalPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

        await file.CopyToAsync(
            stream,
            cancellationToken);

        return Path.Combine(
            relativeFolder,
            fileName)
            .Replace("\\", "/");
    }

    public Task DeleteAsync(
        string fileReference,
        CancellationToken cancellationToken)
    {
        var physicalPath = Path.Combine(
            _environment.ContentRootPath,
            fileReference);

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }

        return Task.CompletedTask;
    }
}