using bams.server.Models.Accounts;
using bams.server.Models.Customers;

namespace bams.server.Services.Interfaces;

public interface IAccountRefererService
{
    /// <summary>
    /// Resolves referers by NRC and checks the required count, that none of them is one of the account's own
    /// holders, and that each one holds an account that is not closed.
    /// </summary>
    Task<IReadOnlyList<Customer>> ResolveAndValidateReferersAsync(
        IReadOnlyList<string>? refererNrcs,
        int requiredCount,
        IReadOnlyCollection<long> holderCustomerIds,
        CancellationToken cancellationToken);

    Task CreateAccountReferersAsync(
        Account account,
        IReadOnlyList<Customer> referers,
        CancellationToken cancellationToken);
}
