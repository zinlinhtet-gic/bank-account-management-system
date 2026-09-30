using bams.desktop.Api;
using bams.desktop.DTOs.Accounting;

namespace bams.desktop.Services;

public sealed class AccountingService : IAccountingService
{
    private readonly ApiClient _apiClient;
    public AccountingService(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }
    public Task<IReadOnlyList<GlAccountResponse>> GetGlAccountsAsync(CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<IReadOnlyList<GlAccountResponse>>(
            "/api/accounting/gl-accounts",
            cancellationToken
        );
    }
    public Task<GlAccountResponse> GetGlAccountByIdAsync(long glAccountId, CancellationToken cancellationToken)
    {
        return _apiClient.GetAsync<GlAccountResponse>(
            $"/api/accounting/gl-accounts/{glAccountId}",
            cancellationToken
        );
    }
    public Task<IReadOnlyList<AccountingEntryResponse>> GetAccountingEntriesAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        long? glAccountId,
        EntryType? entryType,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();
        if (fromDate.HasValue)
        {
            query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        }
        if (toDate.HasValue)
        {
            query.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        }
        if (glAccountId.HasValue)
        {
            query.Add($"glAccountId={glAccountId.Value}");
        }
        if (entryType.HasValue)
        {
            query.Add($"entryType={entryType.Value}");
        }
        var endpoint = "/api/accounting/entries";
        if (query.Count > 0)
        {
            endpoint += "?" + string.Join("&", query);
        }
        return _apiClient.GetAsync<IReadOnlyList<AccountingEntryResponse>>(
            endpoint,
            cancellationToken);
    }
}