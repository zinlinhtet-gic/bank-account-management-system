using bams.server.DTO.Customers;

namespace bams.server.Services.Interfaces;

public interface ICustomerService
{
    /// <summary>
    /// Gets all customer summaries.
    /// </summary>
    Task<IReadOnlyList<CustomerSummaryResponse>> GetCustomersAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a single customer, including its documents, by unique identifier.
    /// </summary>
    Task<CustomerResponse> GetCustomerByIdAsync(
        long id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new customer from the API request contract.
    /// </summary>
    Task<CustomerResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken);
}
