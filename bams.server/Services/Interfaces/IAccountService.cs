using bams.server.DTO.Accounts;

namespace bams.server.Services.Interfaces;

public interface IAccountService
{
    /// <summary>
    /// Gets all account summaries.
    /// </summary>
    Task<IReadOnlyList<AccountSummaryResponse>> GetAccountsAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets a single account by its unique identifier.
    /// </summary>
    Task<AccountResponse> GetAccountByIdAsync(
        long id,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new account from the API request contract.
    /// </summary>
    Task<AccountResponse> CreateAccountAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken);

}
