using bams.server.Models.Accounts;
using bams.server.Models.Customers;

namespace bams.server.Services.Interfaces;

public interface IAccountRefererService
{
    Task<IReadOnlyList<Customer>> ResolveAndValidateReferersAsync(
        IReadOnlyList<string>? refererNrcs,
        int requiredCount,
        CancellationToken cancellationToken);

    Task CreateAccountReferersAsync(
        Account account,
        IReadOnlyList<Customer> referers,
        CancellationToken cancellationToken);
}
