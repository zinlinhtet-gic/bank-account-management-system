using bams.server.DTO.Accounts;
using bams.server.Models.Customers;

namespace bams.server.Services.Interfaces;

/// <summary>Provides the customer lookups used by account-opening flows.</summary>
public interface ICustomerLookUpService
{
    /// <summary>Finds an existing customer by NRC and maps the public lookup response.</summary>
    Task<CustomerLookupResponse> GetCustomerByNrcAsync(string nrc, CancellationToken cancellationToken);

    /// <summary>Finds an existing customer entity for internal account workflows.</summary>
    Task<Customer> FindRegisteredCustomerByNrcAsync(string? nrc, CancellationToken cancellationToken);
}
