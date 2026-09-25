using System.Globalization;
using bams.server.Configuration;
using bams.server.Constants;
using bams.server.Exceptions;
using bams.server.Messages;
using Microsoft.Extensions.Options;

namespace bams.server.Utils;

public sealed class FileUploadUtils
{
    private readonly string _storageRoot;
    private readonly FileUploadOptions _options;

    public FileUploadUtils(
        IWebHostEnvironment environment,
        IOptions<FileUploadOptions> options)
    {
        _options = options.Value;
        _storageRoot = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, _options.RootPath));
    }

    /// <summary>
    /// Validates and stores an uploaded account document using a generated GUID filename.
    /// </summary>
    public async Task<string> SaveAccountDocumentAsync(
        long accountId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await ValidateFileAsync(file, cancellationToken);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var relativeReference = Path.Combine(
            "accounts",
            accountId.ToString(CultureInfo.InvariantCulture),
            storedFileName);
        var absolutePath = ResolveStoragePath(relativeReference);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
            await using var outputStream = new FileStream(
                absolutePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            await file.CopyToAsync(outputStream, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            DeleteAbsoluteFileIfPresent(absolutePath);
            throw new FileStorageException(MessageCode.FileStorageFailed, exception);
        }
        catch
        {
            DeleteAbsoluteFileIfPresent(absolutePath);
            throw;
        }

        return relativeReference.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>
    /// Deletes a previously stored file reference during rollback cleanup.
    /// </summary>
    public void DeleteFile(string fileReference)
    {
        var absolutePath = ResolveStoragePath(fileReference);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }

    /// <summary>
    /// Validates file length, extension, content type, and leading file signature.
    /// </summary>
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

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!DocumentConstants.AllowedContentTypes.TryGetValue(extension, out var contentType) ||
            !string.Equals(file.ContentType, contentType, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(MessageCode.UnsupportedDocumentFileType);
        }

        await using var stream = file.OpenReadStream();
        var signature = new byte[8];
        var bytesRead = await stream.ReadAsync(signature, cancellationToken);
        if (!HasValidSignature(extension, signature.AsSpan(0, bytesRead)))
        {
            throw new ValidationException(MessageCode.InvalidDocumentFileContent);
        }
    }

    // Removes a partially written file without masking the original storage failure.
    private static void DeleteAbsoluteFileIfPresent(string absolutePath)
    {
        try
        {
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }
        catch
        {
            // Best-effort cleanup is intentionally silent here; the original exception is preserved.
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

    // Resolves a reference and prevents path traversal outside the configured storage root.
    private string ResolveStoragePath(string fileReference)
    {
        var absolutePath = Path.GetFullPath(Path.Combine(_storageRoot, fileReference));
        var rootPrefix = _storageRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!absolutePath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(MessageCode.InvalidDocumentFileReference);
        }

        return absolutePath;
    }
}
