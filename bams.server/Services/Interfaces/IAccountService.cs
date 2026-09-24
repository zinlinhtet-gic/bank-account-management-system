using bams.server.DTO.Accounts;
using bams.server.DTO.Common;
using bams.server.Models.Accounts.Enums;

namespace bams.server.Services.Interfaces;

public interface IAccountService
{
    /// <summary>
    /// Gets a forward-only cursor page of account summaries matching the supplied criteria.
    /// </summary>
    Task<CursorPagedResponse<AccountSummaryResponse>> GetAccountsAsync(
        GetAccountsRequest request,
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

    /// <summary>
    /// Updates the status of an existing account and records the responsible user.
    /// </summary>
    Task<AccountResponse> UpdateAccountStatusAsync(
        long id,
        AccountStatus status,
        string? reason,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies an adjustment to an existing account's balance.
    /// </summary>
    Task<AccountResponse> UpdateAccountBalanceAsync(
        long id,
        decimal balanceAdjustment,
        long expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the editable details of an account's existing joint holders.
    /// </summary>
    Task<IReadOnlyList<AccountHolderResponse>> UpdateHoldersOfAccountAsync(
        long accountId,
        UpdateAccountHoldersRequest updateRequest,
        CancellationToken cancellationToken);

}
