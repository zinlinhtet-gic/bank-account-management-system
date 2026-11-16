using System.Globalization;
using bams.server.Configuration;
using bams.server.Constants;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace bams.server.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly FileUploadOptions _options;

    public LocalFileStorageService(
        IWebHostEnvironment environment,
        IOptions<FileUploadOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<string> SaveAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken)
    {
        return WriteValidatedFileAsync(file, folder, cancellationToken);
    }

    /// <inheritdoc />
    public Task<string> SaveAccountDocumentAsync(
        long accountId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        // Account documents are grouped by the immutable database identifier of their account.
        var folder = Path.Combine(
            DocumentConstants.AccountDocumentFolderName,
            accountId.ToString(CultureInfo.InvariantCulture));

        return WriteValidatedFileAsync(file, folder, cancellationToken);
    }

    // Validates the file, then writes it under the uploads root with a generated GUID name.
    private async Task<string> WriteValidatedFileAsync(
        IFormFile file,
        string folder,
        CancellationToken cancellationToken)
    {
        await ValidateFileAsync(file, cancellationToken);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var relativeReference = Path.Combine(
            DocumentConstants.UploadRootFolderName,
            folder,
            $"{Guid.NewGuid():N}{extension}");
        var physicalPath = GetPhysicalPath(relativeReference);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!);

            await using var stream = new FileStream(
                physicalPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: DocumentConstants.FileCopyBufferSizeBytes,
                useAsync: true);

            await file.CopyToAsync(stream, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DeletePartialFileIfPresent(physicalPath);
            throw new FileStorageException(MessageCode.FileStorageFailed, exception);
        }
        catch
        {
            DeletePartialFileIfPresent(physicalPath);
            throw;
        }

        return relativeReference.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <inheritdoc />
    public async Task ValidateFileAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException(MessageCode.UploadedFileEmpty);
        }

        if (file.Length > _options.MaximumFileSizeBytes)
        {
            throw new ValidationException(MessageCode.UploadedFileTooLarge);
        }

        // The extension and declared content type must both be on the allow-list and agree.
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!DocumentConstants.AllowedContentTypes.TryGetValue(extension, out var contentType) ||
            !string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(MessageCode.UnsupportedDocumentFileType);
        }

        // The leading bytes must match the format the extension claims.
        await using var stream = file.OpenReadStream();
        var signature = new byte[DocumentConstants.FileSignatureLength];
        var bytesRead = await stream.ReadAsync(signature, cancellationToken);
        if (!HasValidSignature(extension, signature.AsSpan(0, bytesRead)))
        {
            throw new ValidationException(MessageCode.InvalidDocumentFileContent);
        }
    }

    /// <inheritdoc />
    public Task DeleteAsync(
        string fileReference,
        CancellationToken cancellationToken)
    {
        DeleteFile(fileReference);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void DeleteFile(string fileReference)
    {
        var physicalPath = GetPhysicalPath(fileReference);

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }

    /// <inheritdoc />
    public string GetPhysicalPath(string fileReference)
    {
        // Every reference must resolve inside the uploads root to prevent path traversal.
        var root = Path.GetFullPath(Path.Combine(
            _environment.ContentRootPath,
            DocumentConstants.UploadRootFolderName)) + Path.DirectorySeparatorChar;
        var physicalPath = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, fileReference));
        if (!physicalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(MessageCode.InvalidDocumentFileReference);
        return physicalPath;
    }

    // Removes a partially written file without masking the original storage failure.
    private static void DeletePartialFileIfPresent(string physicalPath)
    {
        try
        {
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
        catch
        {
            // Best-effort cleanup is intentionally silent; the original exception is preserved.
        }
    }

    // Matches the known leading bytes for each supported document format.
    private static bool HasValidSignature(string extension, ReadOnlySpan<byte> signature)
    {
        return extension switch
        {
            ".pdf" => signature.StartsWith("%PDF-"u8),
            ".jpg" or ".jpeg" => signature.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
            ".png" => signature.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            _ => false
        };
    }
}
