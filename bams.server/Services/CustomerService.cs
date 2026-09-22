using System.Text.RegularExpressions;
using bams.server.Constants;
using bams.server.Data;
using bams.server.DTO.Customers;
using bams.server.Exceptions;
using bams.server.Mapping;
using bams.server.Messages;
using bams.server.Models.Customers;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

public sealed class CustomerService : ICustomerService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<CustomerService> _logger;
    private readonly CustomerNumberGenerator _customerNumberGenerator;

    public CustomerService(
        ApplicationDbContext dbContext,
        IFileStorageService fileStorageService,
        ILogger<CustomerService> logger,
        CustomerNumberGenerator customerNumberGenerator)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
        _logger = logger;
        _customerNumberGenerator = customerNumberGenerator;
    }

    /// <summary>
    /// Gets all customer summaries using a read-only database query.
    /// </summary>
    public async Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomersAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.Customers
            .AsNoTracking()
            .OrderBy(customer => customer.Id)
            .Select(customer => new CustomerSummaryResponse(
                customer.Id,
                customer.CustomerNo,
                customer.CustomerType,
                customer.FullName,
                customer.Phone,
                customer.Email,
                customer.KycStatus,
                customer.Status))
            .ToListAsync(cancellationToken);
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
        ValidateRequest(request);
        await EnsureCustomerIsUniqueAsync(request, cancellationToken);

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

    // Validates request fields and simple business rules that do not require a database lookup.
    private static void ValidateRequest(CreateCustomerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ValidationException(MessageCode.CustomerFullNameRequired);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Reject a birth date that has not happened yet.
        if (request.DateOfBirth > today)
        {
            throw new ValidationException(MessageCode.CustomerDateOfBirthInvalid);
        }

        // Enforce the minimum age required to open a customer profile.
        if (CalculateAge(request.DateOfBirth, today) < CustomerConstants.MinimumAgeYears)
        {
            throw new ValidationException(MessageCode.CustomerBelowMinimumAge);
        }

        // Citizens are identified by NRC number; foreigners by passport number.
        if (request.CustomerType == CustomerType.Citizen && string.IsNullOrWhiteSpace(request.NrcNumber))
        {
            throw new ValidationException(MessageCode.NrcNumberRequired);
        }

        if (request.CustomerType == CustomerType.Foreigner && string.IsNullOrWhiteSpace(request.PassportNumber))
        {
            throw new ValidationException(MessageCode.PassportNumberRequired);
        }

        if (request.Email is not null && !Regex.IsMatch(request.Email, CustomerConstants.CustomerEmailRegexPattern))
        {
            throw new ValidationException(MessageCode.CustomerEmailInvalid);
        }
    }

    // Rejects a request that would duplicate an existing customer's NRC number, passport number, or email.
    private async Task EnsureCustomerIsUniqueAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        var hasDuplicate = await _dbContext.Customers
            .AsNoTracking()
            .AnyAsync(
                existing =>
                    (request.NrcNumber != null && existing.NrcNumber == request.NrcNumber) ||
                    (request.PassportNumber != null && existing.PassportNumber == request.PassportNumber) ||
                    (request.Email != null && existing.Email == request.Email),
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
}
