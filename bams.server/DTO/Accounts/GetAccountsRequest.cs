using bams.server.Constants;
using bams.server.Models.Accounts.Enums;

namespace bams.server.DTO.Accounts;

/// <summary>
/// Defines cursor pagination, account-number search, and filters for the account list.
/// </summary>
public sealed class GetAccountsRequest
{
    public string? Cursor { get; init; }

    public int PageSize { get; init; } = AccountConstants.DefaultAccountPageSize;

    public string? Search { get; init; }

    public long? AccountTypeId { get; init; }

    public AccountStatus? Status { get; init; }
}
