using bams.server.DTO.Common;
using bams.server.DTO.Customers;

namespace bams.server.Services.Interfaces;

public interface ICustomerService
{
    /// <summary>
    /// Gets a page of customer summaries, optionally filtered by customer number, name,
    /// KYC status, status, or risk level. Page size is fixed by <c>CustomerConstants.CustomersPageSize</c>.
    /// </summary>
    Task<PagedResponse<CustomerSummaryResponse>> GetCustomersAsync(
        GetCustomersRequest request,
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

    /// <summary>
    /// Partially updates a customer: only properties supplied (non-null) in the request are
    /// changed, everything else keeps its current value. Document entries with an Id edit an
    /// existing document (optionally replacing its file); entries without one add a new document.
    /// </summary>
    Task<CustomerResponse> UpdateCustomerAsync(
        long id,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a KYC review decision for a customer. Approving (Verified) stamps every one of
    /// the customer's documents as verified (sets each document's VerifiedAt/VerifiedBy);
    /// rejecting only changes the customer's KycStatus.
    /// </summary>
    Task<CustomerResponse> ReviewCustomerKycAsync(
        long id,
        ReviewCustomerKycRequest request,
        CancellationToken cancellationToken);
}
