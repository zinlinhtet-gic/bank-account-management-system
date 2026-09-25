using bams.desktop.DTOs.Accounts;

namespace bams.desktop.Services;

/// <summary>
/// Account API calls (<c>api/accounts</c>, permission <c>account_management</c>).
/// </summary>
public interface IAccountService
{
    /// <summary>Loads every account summary, used by the transaction forms' account pickers.</summary>
    Task<IReadOnlyList<AccountSummaryResponse>> GetAccountsAsync(CancellationToken cancellationToken);
}
