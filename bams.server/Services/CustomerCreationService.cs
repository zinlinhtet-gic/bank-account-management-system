using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.DTO.Customers;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Customers;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Validates and persists customers registered from account opening.</summary>
public sealed class CustomerCreationService : ICustomerCreationService
{
    private const string DefaultCustomerStatus = "Active";
    private const string DefaultNationality = "Myanmar";
    private const string CustomerNumberPrefix = "C";
    private readonly ApplicationDbContext _dbContext;

    public CustomerCreationService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<CustomerLookupResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DateOfBirth == DateOnly.MinValue || request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ValidationException(MessageCode.InvalidRequest);
        }

        if (!Enum.IsDefined(request.CustomerType))
        {
            throw new ValidationException(MessageCode.InvalidRequest);
        }

        var normalizedNrc = request.NrcNumber.Trim();
        var nrcAlreadyExists = await _dbContext.Customers
            .AnyAsync(customer => customer.NrcNumber == normalizedNrc, cancellationToken);

        if (nrcAlreadyExists)
        {
            throw new ConflictException(MessageCode.CustomerNrcAlreadyExists);
        }

        // CustomerNo is an internal identifier and must be unique without requiring a sequence migration.
        var createdAt = DateTime.UtcNow;
        var customer = new Customer
        {
            CustomerNo = CustomerNumberPrefix + Guid.NewGuid().ToString("N")[..31],
            CustomerType = request.CustomerType,
            FullName = request.FullName.Trim(),
            DateOfBirth = request.DateOfBirth,
            Nationality = DefaultNationality,
            NrcNumber = normalizedNrc,
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            RiskLevel = RiskLevel.Low,
            KycStatus = KycStatus.Pending,
            Status = DefaultCustomerStatus,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        _dbContext.Customers.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CustomerLookupResponse(
            customer.Id,
            customer.CustomerNo,
            customer.FullName,
            customer.DateOfBirth,
            customer.NrcNumber,
            customer.Phone,
            customer.Email,
            customer.Status,
            customer.CustomerType);
    }
}
