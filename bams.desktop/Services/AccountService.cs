using System.Globalization;
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

    public async Task<IReadOnlyList<AccountSummaryResponse>> GetAccountsAsync(CancellationToken cancellationToken)
    {
        var accounts = new List<AccountSummaryResponse>();
        string? cursor = null;

        // The server returns a forward-only cursor page (no envelope); follow NextCursor until the last page.
        do
        {
            var page = await _apiClient.GetRawAsync<AccountPageResponse>(BuildAccountPageEndpoint(cursor), cancellationToken);
            accounts.AddRange(page.Items);
            cursor = page.HasMore ? page.NextCursor : null;
        }
        while (!string.IsNullOrEmpty(cursor));

        return accounts;
    }

    // Builds the accounts query for one picker page, continuing after the given cursor when there is one.
    private static string BuildAccountPageEndpoint(string? cursor)
    {
        var endpoint = $"{ApiConstants.AccountsEndpoint}?pageSize={ApiConstants.AccountPickerPageSize.ToString(CultureInfo.InvariantCulture)}";

        return cursor is null ? endpoint : $"{endpoint}&cursor={Uri.EscapeDataString(cursor)}";
    }
}
