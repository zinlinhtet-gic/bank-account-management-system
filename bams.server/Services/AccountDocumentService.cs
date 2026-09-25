using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Accounts;
using bams.server.Services.Interfaces;
using bams.server.Utils;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class AccountDocumentService : IAccountDocumentService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly FileUploadUtils _fileUploadUtils;
    private readonly ILogger<AccountDocumentService> _logger;

    public AccountDocumentService(
        ApplicationDbContext dbContext,
        FileUploadUtils fileUploadUtils,
        ILogger<AccountDocumentService> logger)
    {
        _dbContext = dbContext;
        _fileUploadUtils = fileUploadUtils;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ValidateRequiredDocumentsAsync(
        long accountTypeId,
        IReadOnlyList<AccountDocumentUploadRequest> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Any(document => document.File is null))
        {
            throw new ValidationException(MessageCode.UploadedFileEmpty);
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
            if (Path.GetFileName(document.File.FileName).Length >
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
        DateTime uploadedAt,
        CancellationToken cancellationToken)
    {
        var storedReferences = new List<string>();

        try
        {
            foreach (var document in documents)
            {
                var fileReference = await _fileUploadUtils.SaveAccountDocumentAsync(
                    account.Id,
                    document.File,
                    cancellationToken);
                storedReferences.Add(fileReference);

                await _dbContext.AccountDocuments.AddAsync(
                    new AccountDocument
                    {
                        AccountId = account.Id,
                        DocumentType = document.DocumentType,
                        DocumentNumber = document.DocumentNumber,
                        OriginalFileName = Path.GetFileName(document.File.FileName),
                        FileReference = fileReference,
                        ContentType = document.File.ContentType,
                        FileSizeBytes = document.File.Length,
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
