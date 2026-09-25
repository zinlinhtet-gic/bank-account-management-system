using bams.server.DTO.Accounts;
using bams.server.DTO.Customers;

namespace bams.server.Services.Interfaces;

/// <summary>Creates persisted customer records.</summary>
public interface ICustomerCreationService
{
    Task<CustomerLookupResponse> CreateCustomerAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken);
}
