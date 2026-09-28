using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounts;

namespace bams.desktop.Services;

/// <summary>
/// Account API calls. Transport, token and error translation are handled by <see cref="ApiClient"/>.
/// </summary>
public sealed class AccountService : IAccountService
{
    private readonly ApiClient _apiClient;

    public AccountService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public Task<IReadOnlyList<AccountSummaryResponse>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<AccountSummaryResponse>>(ApiConstants.AccountsEndpoint, cancellationToken);
    }
}
