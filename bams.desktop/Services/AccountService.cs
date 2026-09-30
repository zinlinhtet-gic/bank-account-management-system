using bams.desktop.Api;
using bams.desktop.Constants;
using bams.desktop.DTOs.Accounts;

namespace bams.desktop.Services;

/// <summary>
/// Account API calls. Transport, token and error translation are handled by <see cref="ApiClient"/>.
/// </summary>
public sealed class AccountService : IAccountService
{
    private const int AccountPickerPageSize = 100;
    private readonly ApiClient _apiClient;

    public AccountService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<IReadOnlyList<AccountSummaryResponse>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = new List<AccountSummaryResponse>();
        string? cursor = null;

        do
        {
            var query = $"?pageSize={AccountPickerPageSize}";
            if (!string.IsNullOrWhiteSpace(cursor))
            {
                query += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var page = await _apiClient.GetAsync<AccountPageResponse>(
                ApiConstants.AccountsEndpoint + query,
                cancellationToken);
            accounts.AddRange(page.Items);
            cursor = page.HasMore ? page.NextCursor : null;
        }
        while (!string.IsNullOrWhiteSpace(cursor));

        return accounts;
    }
}
