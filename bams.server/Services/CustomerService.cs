using System.Security.Claims;
using System.Text.RegularExpressions;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Common;
using bams.server.DTO.Customers;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Customers;
using bams.server.Models.Security;
using bams.server.Services.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<CustomerService> _logger;
    private readonly CustomerNumberGenerator _customerNumberGenerator;

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomerService(
        ApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<CustomerService> logger,
        CustomerNumberGenerator customerNumberGenerator,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _logger = logger;
        _customerNumberGenerator = customerNumberGenerator;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets a page of customer summaries using a read-only database query, applying whichever
    /// filters were supplied (customer number, name, KYC status, status, risk level).
    /// </summary>
    public async Task<PagedResponse<CustomerSummaryResponse>> GetCustomersAsync(
        GetCustomersRequest request,
        CancellationToken cancellationToken)
    {
        var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

        var query = BuildCustomerFilterQuery(request);

        var totalCount = await query.CountAsync(cancellationToken);

        var customers = await query
            .OrderBy(customer => customer.Id)
            .Skip((pageNumber - 1) * CustomerConstants.CustomersPageSize)
            .Take(CustomerConstants.CustomersPageSize)
            .Include(customer => customer.Documents)
            .Select(customer => new CustomerSummaryResponse(
                customer.Id,
                customer.CustomerNo,
                customer.CustomerType,
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.KycStatus,
                customer.RiskLevel,
                customer.Status,
                customer.CreatedAt,
                customer.Documents.ToList()))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)CustomerConstants.CustomersPageSize);

        return new PagedResponse<CustomerSummaryResponse>(
            customers,
            pageNumber,
            CustomerConstants.CustomersPageSize,
            totalCount,
            totalPages);
    }



    /// <summary>
    /// Gets a single customer, including its documents, by unique identifier.
    /// </summary>
    public async Task<CustomerResponse> GetCustomerByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .AsNoTracking()
            .Include(customer => customer.Documents)
            .FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException(MessageCode.CustomerNotFound);
        }

        return customer.ToResponse();
    }

    /// <summary>
    /// Creates a customer, its identity documents, and any uploaded files as a single unit of work.
    /// </summary>
    public async Task<CustomerResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCustomerFields(
            request.CustomerType,
            request.FullName,
            request.DateOfBirth,
            request.NrcNumber,
            request.PassportNumber,
            request.Email);

        await EnsureCustomerIsUniqueAsync(
            request.NrcNumber,
            request.PassportNumber,
            request.Email,
            excludeCustomerId: null,
            cancellationToken);

        var customerNo = await _customerNumberGenerator.GenerateAsync(cancellationToken);
        var customer = BuildCustomer(request, customerNo);

        // Files are written to disk outside the database transaction, so track them
        // here and delete them if the transaction below has to roll back.
        var savedFileReferences = new List<string>();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await AttachCustomerDocumentsAsync(customer, request.Documents, savedFileReferences, cancellationToken);

            _dbContext.Customers.Add(customer);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return customer.ToResponse();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await DeleteSavedFilesAsync(savedFileReferences, cancellationToken);

            throw;
        }
    }

    /// <summary>
    /// Partially updates a customer's fields and documents as a single unit of work: only
    /// supplied fields change, document entries with an Id edit an existing document
    /// (optionally replacing its file), and entries without one add a new document.
    /// </summary>
    public async Task<CustomerResponse> UpdateCustomerAsync(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _dbContext.Customers
            .Include(customer => customer.Documents)
            .FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException(MessageCode.CustomerNotFound);
        }

        // Merge: a supplied (non-null) value replaces the existing one; an omitted
        // (null) value keeps whatever the customer already has.
        var customerType = request.CustomerType ?? customer.CustomerType;
        var fullName = request.FullName ?? customer.FullName;
        var dateOfBirth = request.DateOfBirth ?? customer.DateOfBirth;
        var nrcNumber = request.NrcNumber ?? customer.NrcNumber;
        var passportNumber = request.PassportNumber ?? customer.PassportNumber;
        var email = request.Email ?? customer.Email;

        ValidateCustomerFields(customerType, fullName, dateOfBirth, nrcNumber, passportNumber, email);
        await EnsureCustomerIsUniqueAsync(nrcNumber, passportNumber, email, customer.Id, cancellationToken);
        ValidateDocumentChangeRequests(customer, request.Documents);

        customer.CustomerType = customerType;
        customer.FullName = fullName.Trim();
        customer.DateOfBirth = dateOfBirth;
        customer.NrcNumber = nrcNumber;
        customer.PassportNumber = passportNumber;
        customer.Email = email;
        customer.Nationality = request.Nationality ?? customer.Nationality;
        customer.Phone = request.Phone ?? customer.Phone;
        customer.Occupation = request.Occupation ?? customer.Occupation;
        customer.AddressLine1 = request.AddressLine1 ?? customer.AddressLine1;
        customer.AddressLine2 = request.AddressLine2 ?? customer.AddressLine2;
        customer.City = request.City ?? customer.City;
        customer.State = request.State ?? customer.State;
        customer.PostalCode = request.PostalCode ?? customer.PostalCode;
        customer.Country = request.Country ?? customer.Country;
        customer.UpdatedAt = DateTime.UtcNow;

        // New files are written to disk outside the database transaction, so track them for
        // rollback cleanup; a replaced document's old file is only deleted after commit succeeds,
        // so a failed save never leaves a document pointing at a file that no longer exists.
        var newFileReferences = new List<string>();
        var replacedFileReferences = new List<string>();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await ApplyCustomerDocumentChangesAsync(
                customer,
                request.Documents,
                newFileReferences,
                replacedFileReferences,
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await DeleteSavedFilesAsync(replacedFileReferences, cancellationToken);

            return customer.ToResponse();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await DeleteSavedFilesAsync(newFileReferences, cancellationToken);

            throw;
        }
    }

    /// <summary>
    /// Records a KYC review decision for a customer. Approving stamps every one of the
    /// customer's documents as verified; rejecting only changes the customer's KycStatus.
    /// </summary>
    public async Task<CustomerResponse> ReviewCustomerKycAsync(
        long id,
        ReviewCustomerKycRequest request,
        CancellationToken cancellationToken)
    {
        // A review always makes a decision; Pending is the default state, not a reviewable outcome.
        if (request.KycStatus != KycStatus.Verified && request.KycStatus != KycStatus.Rejected)
        {
            throw new ValidationException(MessageCode.InvalidKycReviewStatus);
        }

        var reviewerIdClaim = _httpContextAccessor.HttpContext?
        .User
        .FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(reviewerIdClaim, out var reviewerId))
        {
            throw new NotFoundException(
                MessageCode.KycReviewerNotFound);
        }

        var customer = await _dbContext.Customers
            .Include(customer => customer.Documents)
            .FirstOrDefaultAsync(customer => customer.Id == id, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException(MessageCode.CustomerNotFound);
        }

        customer.KycStatus = request.KycStatus;
        customer.UpdatedAt = DateTime.UtcNow;

        // Approval verifies every document currently on file; rejection leaves documents untouched.
        if (request.KycStatus == KycStatus.Verified)
        {
            var reviewedAt = DateTime.UtcNow;
            customer.Status = "Active";

            foreach (var document in customer.Documents)
            {
                document.VerifiedAt = reviewedAt;
                document.VerifiedBy = reviewerId;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return customer.ToResponse();
    }

    // Validates the resolved customer fields and simple business rules that do not require a database lookup.
    // Shared by create (request fields) and update (request fields merged over the existing customer).
    private static void ValidateCustomerFields(
        CustomerType customerType,
        string fullName,
        DateOnly dateOfBirth,
        string? nrcNumber,
        string? passportNumber,
        string? email)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new ValidationException(MessageCode.CustomerFullNameRequired);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Reject a birth date that has not happened yet.
        if (dateOfBirth > today)
        {
            throw new ValidationException(MessageCode.CustomerDateOfBirthInvalid);
        }

        // Enforce the minimum age required to open a customer profile.
        if (CalculateAge(dateOfBirth, today) < CustomerConstants.MinimumAgeYears)
        {
            throw new ValidationException(MessageCode.CustomerBelowMinimumAge);
        }

        // Citizens are identified by NRC number; foreigners by passport number.
        if (customerType == CustomerType.Citizen && string.IsNullOrWhiteSpace(nrcNumber))
        {
            throw new ValidationException(MessageCode.NrcNumberRequired);
        }

        if (customerType == CustomerType.Foreigner && string.IsNullOrWhiteSpace(passportNumber))
        {
            throw new ValidationException(MessageCode.PassportNumberRequired);
        }

        if (email is not null && !Regex.IsMatch(email, CustomerConstants.CustomerEmailRegexPattern))
        {
            throw new ValidationException(MessageCode.CustomerEmailInvalid);
        }
    }

    // Rejects a request that would duplicate another existing customer's NRC number, passport number, or
    // email. On update, excludeCustomerId keeps the customer being edited from conflicting with itself.
    private async Task EnsureCustomerIsUniqueAsync(
        string? nrcNumber,
        string? passportNumber,
        string? email,
        long? excludeCustomerId,
        CancellationToken cancellationToken)
    {
        var hasDuplicate = await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                existing =>
                    existing.Id != excludeCustomerId &&
                    ((nrcNumber != null && existing.NrcNumber == nrcNumber) ||
                     (passportNumber != null && existing.PassportNumber == passportNumber) ||
                     (email != null && existing.Email == email)),
                cancellationToken);

        if (hasDuplicate)
        {
            throw new ConflictException(MessageCode.CustomerAlreadyExists);
        }
    }

    // Builds the Customer entity from the request, applying onboarding defaults.
    private static Customer BuildCustomer(CreateCustomerRequest request, string customerNo)
    {
        var now = DateTime.UtcNow;

        return new Customer
        {
            CustomerNo = customerNo,
            CustomerType = request.CustomerType,
            FullName = request.FullName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Nationality = request.Nationality,
            NrcNumber = request.NrcNumber,
            PassportNumber = request.PassportNumber,
            Phone = request.Phone,
            Occupation = request.Occupation,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode,
            Country = request.Country,
            Email = request.Email,
            RiskLevel = RiskLevel.Low,
            KycStatus = KycStatus.Pending,
            Status = CustomerConstants.DefaultStatus,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // Saves each document's uploaded file (if any) and attaches the resulting CustomerDocument
    // entities to the customer. Saved file references are recorded so the caller can delete
    // them if the surrounding transaction is rolled back.
    private async Task AttachCustomerDocumentsAsync(
        Customer customer,
        List<CreateCustomerDocumentRequest>? documentRequests,
        List<string> savedFileReferences,
        CancellationToken cancellationToken)
    {
        if (documentRequests is null || documentRequests.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Processing {DocumentCount} document(s) for new customer {CustomerNo}",
            documentRequests.Count,
            customer.CustomerNo);

        foreach (var documentRequest in documentRequests)
        {
            string? fileReference = null;

            if (documentRequest.File is not null)
            {
                fileReference = await _fileStorageService.SaveAsync(
                    documentRequest.File,
                    CustomerConstants.DocumentUploadFolder,
                    cancellationToken);

                savedFileReferences.Add(fileReference);
            }

            customer.Documents.Add(new CustomerDocument
            {
                DocumentType = documentRequest.DocumentType,
                DocumentNumber = documentRequest.DocumentNumber,
                FileReference = fileReference,
                IssuedDate = documentRequest.IssuedDate,
                ExpiryDate = documentRequest.ExpiryDate
            });
        }
    }

    // Validates document change requests before any file I/O: every Id must reference a document
    // the customer already has, and a new document (no Id) must specify a document type.
    private static void ValidateDocumentChangeRequests(
        Customer customer,
        List<UpdateCustomerDocumentRequest>? documentRequests)
    {
        if (documentRequests is null)
        {
            return;
        }

        foreach (var documentRequest in documentRequests)
        {
            if (documentRequest.Id is long documentId)
            {
                var documentExists = customer.Documents.Any(document => document.Id == documentId);

                if (!documentExists)
                {
                    throw new NotFoundException(MessageCode.CustomerDocumentNotFound);
                }
            }
            else if (documentRequest.DocumentType is null)
            {
                throw new ValidationException(MessageCode.CustomerDocumentTypeRequired);
            }
        }
    }

    // Adds new documents (no Id) and edits existing ones (Id set) on the customer, saving any
    // uploaded files. New file references are recorded for rollback cleanup on failure; a
    // replaced document's previous file is recorded for cleanup only after a successful commit.
    private async Task ApplyCustomerDocumentChangesAsync(
        Customer customer,
        List<UpdateCustomerDocumentRequest>? documentRequests,
        List<string> newFileReferences,
        List<string> replacedFileReferences,
        CancellationToken cancellationToken)
    {
        if (documentRequests is null || documentRequests.Count == 0)
        {
            return;
        }

        foreach (var documentRequest in documentRequests)
        {
            string? savedFileReference = null;

            if (documentRequest.File is not null)
            {
                savedFileReference = await _fileStorageService.SaveAsync(
                    documentRequest.File,
                    CustomerConstants.DocumentUploadFolder,
                    cancellationToken);

                newFileReferences.Add(savedFileReference);
            }

            if (documentRequest.Id is long documentId)
            {
                // Existing document; ValidateDocumentChangeRequests already confirmed it exists.
                var document = customer.Documents.First(document => document.Id == documentId);

                document.DocumentType = documentRequest.DocumentType ?? document.DocumentType;
                document.DocumentNumber = documentRequest.DocumentNumber ?? document.DocumentNumber;
                document.IssuedDate = documentRequest.IssuedDate ?? document.IssuedDate;
                document.ExpiryDate = documentRequest.ExpiryDate ?? document.ExpiryDate;

                if (savedFileReference is not null)
                {
                    if (document.FileReference is not null)
                    {
                        replacedFileReferences.Add(document.FileReference);
                    }

                    document.FileReference = savedFileReference;
                }
            }
            else
            {
                // ValidateDocumentChangeRequests already confirmed DocumentType is present for new documents.
                customer.Documents.Add(new CustomerDocument
                {
                    DocumentType = documentRequest.DocumentType!.Value,
                    DocumentNumber = documentRequest.DocumentNumber,
                    FileReference = savedFileReference,
                    IssuedDate = documentRequest.IssuedDate,
                    ExpiryDate = documentRequest.ExpiryDate
                });
            }
        }
    }

    // Best-effort cleanup of files already written to storage when the surrounding transaction fails.
    private async Task DeleteSavedFilesAsync(
        IReadOnlyList<string> savedFileReferences,
        CancellationToken cancellationToken)
    {
        foreach (var fileReference in savedFileReferences)
        {
            await _fileStorageService.DeleteAsync(fileReference, cancellationToken);
        }
    }

    // Calculates whole years of age as of the given reference date.
    private static int CalculateAge(DateOnly dateOfBirth, DateOnly asOfDate)
    {
        var age = asOfDate.Year - dateOfBirth.Year;

        if (asOfDate < dateOfBirth.AddYears(age))
        {
            age--;
        }

        return age;
    }

    // Applies whichever customer-list filters were supplied to a read-only query.
    private IQueryable<Customer> BuildCustomerFilterQuery(GetCustomersRequest request)
    {
        var query = _dbContext.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.CustomerNo))
        {
            query = query.Where(customer => customer.CustomerNo.Contains(request.CustomerNo));
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerName))
        {
            query = query.Where(customer => customer.FullName.Contains(request.CustomerName));
        }

        if (request.KycStatus is not null)
        {
            query = query.Where(customer => customer.KycStatus == request.KycStatus);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(customer => customer.Status == request.Status);
        }

        if (request.RiskLevel is not null)
        {
            query = query.Where(customer => customer.RiskLevel == request.RiskLevel);
        }

        // Filter by the customer's created date, inclusive on both ends.
        if (request.StartDate is not null)
        {
            var startDateTime = request.StartDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(customer => customer.CreatedAt >= startDateTime);
        }

        if (request.EndDate is not null)
        {
            var endDateTimeExclusive = request.EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(customer => customer.CreatedAt < endDateTimeExclusive);
        }

        return query;
    }
}
