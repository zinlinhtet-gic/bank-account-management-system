using bams.server.Data;
using bams.server.DTO.Accounts;
using bams.server.Exceptions;
using bams.server.Messages;
using bams.server.Models.Customers;
using bams.server.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace bams.server.Services;

/// <summary>Centralizes customer lookup by NRC for account API and domain services.</summary>
public sealed class CustomerLookUpService : ICustomerLookUpService
{
    private readonly ApplicationDbContext _dbContext;

    public CustomerLookUpService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<CustomerLookupResponse> GetCustomerByNrcAsync(string nrc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nrc))
        {
            throw new ValidationException(MessageCode.InvalidRequest);
        }

        var customer = await FindRegisteredCustomerByNrcAsync(nrc, cancellationToken);
        return new CustomerLookupResponse(customer.Id, customer.CustomerNo, customer.FullName,
            customer.DateOfBirth, customer.NrcNumber, customer.Phone, customer.Email, customer.Status, customer.CustomerType);
    }

    /// <inheritdoc />
    public async Task<Customer> FindRegisteredCustomerByNrcAsync(string? nrc, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(nrc))
        {
            throw new NotFoundException(MessageCode.CustomerNotFound);
        }

        var normalizedNrc = nrc.Trim();
        var customer = await _dbContext.Customers.AsNoTracking()
            .FirstOrDefaultAsync(item => item.NrcNumber == normalizedNrc, cancellationToken);

        return customer ?? throw new NotFoundException(MessageCode.CustomerNotFound);
    }
}
