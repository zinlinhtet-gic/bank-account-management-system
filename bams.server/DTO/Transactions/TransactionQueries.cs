using bams.server.Models.Transactions;

namespace bams.server.DTO.Transactions;

/// <summary>
/// Optional filters for <c>GET api/transactions</c>, read from the query string. Newest transactions come first.
/// </summary>
/// <param name="AccountId">Only transactions that posted an entry to this account.</param>
/// <param name="AccountNo">Only transactions that posted an entry to the account with this number (exact match).</param>
/// <param name="Type">Only this transaction type, e.g. NrcTransfer.</param>
/// <param name="Status">Only this status, e.g. Pending.</param>
/// <param name="From">Only transactions at or after this instant.</param>
/// <param name="Before">Only transactions before this instant (exclusive).</param>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="PageSize">Items per page, at most <c>TransactionConstants.MaximumPageSize</c>.</param>
public sealed record TransactionListQuery(
    long? AccountId,
    string? AccountNo,
    TransactionType? Type,
    TransactionStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? Before,
    int? Page,
    int? PageSize);

/// <summary>
/// Optional filters for an account statement. Newest entries come first.
/// </summary>
/// <param name="From">Only entries at or after this instant.</param>
/// <param name="Before">Only entries before this instant (exclusive).</param>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="PageSize">Items per page, at most <c>TransactionConstants.MaximumPageSize</c>.</param>
public sealed record AccountStatementQuery(
    DateTimeOffset? From,
    DateTimeOffset? Before,
    int? Page,
    int? PageSize);
