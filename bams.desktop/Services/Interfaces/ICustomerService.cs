using bams.desktop.DTOs.Common;
using bams.desktop.DTOs.Customers;

namespace bams.desktop.Services;

/// <summary>
/// Customer Management API calls (<c>api/customers</c>). Listing needs the <c>customer_management</c>
/// or <c>customer_list</c> permission; creating needs <c>customer_management</c>.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Loads one page of customers matching the given filters (all filters optional).
    /// </summary>
    Task<PagedResponse<CustomerSummaryResponse>> GetCustomersAsync(
        GetCustomersRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a customer and returns the saved record.
    /// </summary>
    Task<CustomerResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads a single customer, including its documents, by unique identifier.
    /// </summary>
    Task<CustomerResponse> GetCustomerByIdAsync(
        long id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads one of a customer's documents to a local file path.
    /// </summary>
    Task DownloadCustomerDocumentAsync(
        long customerId,
        long documentId,
        string destinationPath,
        CancellationToken cancellationToken);

    /// <summary>
    /// Downloads one of a customer's documents into memory (e.g. the Photo document, decoded as an
    /// image for the details header).
    /// </summary>
    Task<byte[]> GetCustomerDocumentBytesAsync(
        long customerId,
        long documentId,
        CancellationToken cancellationToken);
}
