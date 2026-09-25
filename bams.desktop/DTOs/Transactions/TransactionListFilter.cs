namespace bams.desktop.DTOs.Transactions;

/// <summary>
/// Filters sent as the query string of <c>GET api/transactions</c>. Null or empty values are left out.
/// </summary>
/// <param name="AccountNo">Only transactions that posted to this account number (exact match).</param>
/// <param name="Type">Only this transaction type; null for all.</param>
/// <param name="Status">Only this status; null for all.</param>
/// <param name="From">Transactions at or after this instant.</param>
/// <param name="Before">Transactions before this instant (exclusive).</param>
public sealed record TransactionListFilter(
    string? AccountNo,
    TransactionType? Type,
    TransactionStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? Before);
