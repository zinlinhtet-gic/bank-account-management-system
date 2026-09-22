using bams.server.DTO.Customers;

namespace bams.server.Services.Interfaces;

public interface ICustomerService
{
    /// <summary>
    /// Creates a new customer from the API request contract.
    /// </summary>
    Task<CustomerResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken);
}
