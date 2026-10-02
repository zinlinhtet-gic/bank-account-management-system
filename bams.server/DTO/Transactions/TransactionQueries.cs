using bams.server.Models.Transactions;

namespace bams.server.DTO.Transactions;

/// <summary>
/// Optional filters for <c>GET api/transactions</c>, read from the query string.
/// Newest transactions come first.
/// </summary>
/// <param name="AccountId">
/// Only transactions that posted an entry to this account.
/// </param>
/// <param name="AccountNo">
/// Only transactions that posted an entry to the account with this number.
/// </param>
/// <param name="TransactionNo">
/// Only the transaction with this transaction number.
/// </param>
/// <param name="ReferenceNo">
/// Only transactions with this reference number.
/// </param>
/// <param name="Type">
/// Only this transaction type.
/// </param>
/// <param name="Status">
/// Only this transaction status.
/// </param>
/// <param name="From">
/// Only transactions at or after this instant.
/// </param>
/// <param name="Before">
/// Only transactions before this instant, exclusive.
/// </param>
/// <param name="Page">
/// Page number, starting at 1.
/// </param>
/// <param name="PageSize">
/// Items per page, limited by the configured transaction paging rules.
/// </param>
public sealed record TransactionListQuery(
    long? AccountId,
    string? AccountNo,
    string? TransactionNo,
    string? ReferenceNo,
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
