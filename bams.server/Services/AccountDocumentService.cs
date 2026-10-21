using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Models.Customers;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AccountDocumentService : IAccountDocumentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly FileUploadUtils _fileUploadUtils;
    private readonly ILogger<AccountDocumentService> _logger;
    private readonly IFileStorageService _customerStorage;

    public AccountDocumentService(
        ApplicationDbContext dbContext,
        FileUploadUtils fileUploadUtils,
        ILogger<AccountDocumentService> logger,
        IFileStorageService customerStorage)
    {
        _dbContext = dbContext;
        _fileUploadUtils = fileUploadUtils;
        _logger = logger;
        _customerStorage = customerStorage;
    }

    /// <inheritdoc />
    public async Task ValidateRequiredDocumentsAsync(
        long accountTypeId,
        IReadOnlyList<AccountDocumentUploadRequest> documents,
        IReadOnlyList<long> holderIds,
        CancellationToken cancellationToken)
    {
        if (documents.Any(document => (document.File is null) == (document.SourceCustomerDocumentId is null)))
        {
            throw new ValidationException(MessageCode.AccountDocumentSourceInvalid);
        }

        if (documents.Any(document => !Enum.IsDefined(document.DocumentType)))
        {
            throw new ValidationException(MessageCode.UnsupportedAccountDocumentType);
        }

        if (documents.GroupBy(document => document.DocumentType).Any(group => group.Count() > 1))
        {
            throw new ValidationException(MessageCode.DuplicateAccountDocumentType);
        }

        foreach (var document in documents)
        {
            if (document.SourceCustomerDocumentId is long sourceId)
            {
                var source = await ResolveSourceAsync(sourceId, document.DocumentType, holderIds, cancellationToken);
                await using var stream = OpenCustomerFile(source);
                await ValidateReusableFileAsync(stream, source, cancellationToken);
                continue;
            }

            if (Path.GetFileName(document.File!.FileName).Length >
                DocumentConstants.OriginalFileNameMaximumLength)
            {
                throw new ValidationException(MessageCode.UploadedFileNameTooLong);
            }

            if (document.DocumentNumber?.Length > DocumentConstants.DocumentNumberMaximumLength)
            {
                throw new ValidationException(MessageCode.AccountDocumentNumberTooLong);
            }

            await _fileUploadUtils.ValidateFileAsync(document.File, cancellationToken);
        }

        var requiredTypes = await _dbContext.AccountTypeRequiredDocuments
            .AsNoTracking()
            .Where(requirement => requirement.AccountTypeId == accountTypeId)
            .Select(requirement => requirement.DocumentType)
            .ToListAsync(cancellationToken);
        var uploadedTypes = documents.Select(document => document.DocumentType).ToHashSet();

        if (requiredTypes.Any(requiredType => !uploadedTypes.Contains(requiredType)))
        {
            throw new ValidationException(MessageCode.RequiredAccountDocumentMissing);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReusableCustomerDocumentResponse>> GetReusableDocumentsAsync(
        IReadOnlyList<long> holderIds, CancellationToken cancellationToken)
    {
        var today = BusinessTime.Today;
        var candidates = await _dbContext.CustomerDocuments.AsNoTracking().Include(document => document.Customer)
            .Where(document => holderIds.Contains(document.CustomerId) && document.FileReference != null &&
                (document.ExpiryDate == null || document.ExpiryDate >= today))
            .ToListAsync(cancellationToken);
        var selected = new List<ReusableCustomerDocumentResponse>();
        foreach (var holderId in holderIds)
        {
            foreach (var document in candidates.Where(document => document.CustomerId == holderId).OrderByDescending(document => document.Id))
            {
                if (!Enum.IsDefined(document.DocumentType) || selected.Any(item => item.DocumentType == document.DocumentType)) continue;
                try
                {
                    await using var stream = OpenCustomerFile(document);
                    await ValidateReusableFileAsync(stream, document, cancellationToken);
                    selected.Add(new(document.Id, document.CustomerId, document.Customer!.FullName,
                        document.DocumentType, document.DocumentNumber, document.ExpiryDate));
                }
                catch (ValidationException)
                {
                    // Unavailable or incompatible customer files leave an upload requirement in the UI.
                }
            }
        }
        return selected;
    }

    // Resolves only a document owned by a current holder and still valid on the bank's business date.
    private async Task<CustomerDocument> ResolveSourceAsync(long id, DocumentType type,
        IReadOnlyList<long> holderIds, CancellationToken cancellationToken)
    {
        var document = await _dbContext.CustomerDocuments.AsNoTracking()
            .SingleOrDefaultAsync(document => document.Id == id && holderIds.Contains(document.CustomerId), cancellationToken);
        if (document is null || document.DocumentType != type || document.ExpiryDate < BusinessTime.Today)
            throw new ValidationException(MessageCode.ReusableAccountDocumentUnavailable);
        return document;
    }

    // Opens the customer-owned source once so validation and copying use the same file handle.
    private FileStream OpenCustomerFile(CustomerDocument document)
    {
        var reference = document.FileReference?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(reference) ||
            reference.Split('/').Any(segment => segment is "." or "..") ||
            !reference.StartsWith($"uploads/{CustomerConstants.DocumentUploadFolder}/", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(MessageCode.ReusableAccountDocumentUnavailable);
        try
        {
            return new FileStream(_customerStorage.GetPhysicalPath(reference), FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 81920, useAsync: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or ValidationException)
        {
            throw new ValidationException(MessageCode.ReusableAccountDocumentUnavailable);
        }
    }

    // Reuses account upload validation instead of accepting weaker historical customer-upload rules.
    private async Task ValidateReusableFileAsync(FileStream stream, CustomerDocument document, CancellationToken cancellationToken)
    {
        try
        {
            await _fileUploadUtils.ValidateFileAsync(CreateSourceUpload(stream, document), cancellationToken);
        }
        catch (Exception exception) when (exception is ValidationException or IOException or UnauthorizedAccessException)
        {
            throw new ValidationException(MessageCode.ReusableAccountDocumentUnavailable);
        }
    }

    // Customer records have no original filename or MIME type; derive safe metadata from their stored extension.
    private static IFormFile CreateSourceUpload(FileStream stream, CustomerDocument document)
    {
        var extension = Path.GetExtension(document.FileReference!).ToLowerInvariant();
        DocumentConstants.AllowedContentTypes.TryGetValue(extension, out var contentType);
        return new FormFile(stream, 0, stream.Length, "File", $"{document.DocumentType}{extension}")
        {
            Headers = new HeaderDictionary(), ContentType = contentType ?? "application/octet-stream"
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountTypeRequiredDocumentResponse>> GetRequiredDocumentsAsync(
        IReadOnlyCollection<long> accountTypeIds,
        CancellationToken cancellationToken)
    {
        if (accountTypeIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.AccountTypeRequiredDocuments.AsNoTracking()
            .Where(requirement => accountTypeIds.Contains(requirement.AccountTypeId))
            .OrderBy(requirement => requirement.AccountTypeId)
            .ThenBy(requirement => requirement.DocumentType)
            .Select(requirement => new AccountTypeRequiredDocumentResponse(
                requirement.AccountTypeId,
                requirement.DocumentType))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> StoreAccountDocumentsAsync(
        Account account,
        IReadOnlyList<AccountDocumentUploadRequest> documents,
        IReadOnlyList<long> holderIds,
        DateTime uploadedAt,
        CancellationToken cancellationToken)
    {
        var storedReferences = new List<string>();

        try
        {
            foreach (var document in documents)
            {
                var source = document.SourceCustomerDocumentId is long sourceId
                    ? await ResolveSourceAsync(sourceId, document.DocumentType, holderIds, cancellationToken) : null;
                await using var sourceStream = source is null ? null : OpenCustomerFile(source);
                if (sourceStream is not null) await ValidateReusableFileAsync(sourceStream, source!, cancellationToken);
                var file = sourceStream is null ? document.File! : CreateSourceUpload(sourceStream, source!);
                var fileReference = await _fileUploadUtils.SaveAccountDocumentAsync(
                    account.Id,
                    file,
                    cancellationToken);
                storedReferences.Add(fileReference);

                await _dbContext.AccountDocuments.AddAsync(
                    new AccountDocument
                    {
                        AccountId = account.Id,
                        SourceCustomerDocumentId = source?.Id,
                        DocumentType = document.DocumentType,
                        DocumentNumber = source is null ? document.DocumentNumber : source.DocumentNumber,
                        OriginalFileName = Path.GetFileName(file.FileName),
                        FileReference = fileReference,
                        ContentType = file.ContentType,
                        FileSizeBytes = file.Length,
                        UploadedAt = uploadedAt,
                        Status = DocumentConstants.PendingVerificationStatus
                    },
                    cancellationToken);
            }

            return storedReferences;
        }
        catch
        {
            DeleteStoredFiles(storedReferences);
            throw;
        }
    }

    /// <inheritdoc />
    public void DeleteStoredFiles(IEnumerable<string> fileReferences)
    {
        foreach (var fileReference in fileReferences)
        {
            try
            {
                _fileUploadUtils.DeleteFile(fileReference);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to delete rolled-back account document {FileReference}", fileReference);
            }
        }
    }
}
