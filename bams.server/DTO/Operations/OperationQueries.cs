using bams.server.Models.Accounts.Enums;
using bams.server.Models.InterestFees;
using bams.server.Models.Products;

namespace bams.server.DTO.Operations;

/// <summary>
/// Optional filters for <c>GET api/operations/interest</c>, read from the query string.
/// </summary>
/// <param name="Search">Matches the account number, or the customer number or name of any account holder.</param>
/// <param name="Status">Only this accrual status: <c>Accrued</c> or <c>Posted</c>.</param>
/// <param name="From">Only accrual periods ending on or after this date.</param>
/// <param name="To">Only accrual periods ending on or before this date.</param>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="PageSize">Items per page, at most <c>TransactionConstants.MaximumPageSize</c>.</param>
public sealed record InterestOperationQuery(
    string? Search,
    string? Status,
    DateOnly? From,
    DateOnly? To,
    int? Page,
    int? PageSize);

/// <summary>
/// Optional filters for <c>GET api/operations/fees</c>, read from the query string.
/// </summary>
/// <param name="Search">Matches the account number, or the customer number or name of any account holder.</param>
/// <param name="FeeType">Only this fee type, e.g. <c>Maintenance</c> or <c>EarlyWithdrawal</c>.</param>
/// <param name="Status">Only this fee accrual status.</param>
/// <param name="From">Only fee periods ending on or after this date.</param>
/// <param name="To">Only fee periods ending on or before this date.</param>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="PageSize">Items per page, at most <c>TransactionConstants.MaximumPageSize</c>.</param>
public sealed record FeeOperationQuery(
    string? Search,
    FeeType? FeeType,
    FeeAccrualStatus? Status,
    DateOnly? From,
    DateOnly? To,
    int? Page,
    int? PageSize);

/// <summary>
/// Optional filters for <c>GET api/operations/fixed-deposit-maturity</c>, read from the query string.
/// </summary>
/// <param name="Search">Matches the account number, or the customer number or name of any account holder.</param>
/// <param name="Status">Only this fixed deposit status.</param>
/// <param name="From">Only deposits maturing on or after this date.</param>
/// <param name="To">Only deposits maturing on or before this date.</param>
/// <param name="Page">Page number, starting at 1.</param>
/// <param name="PageSize">Items per page, at most <c>TransactionConstants.MaximumPageSize</c>.</param>
public sealed record FixedDepositMaturityQuery(
    string? Search,
    FixedDepositStatus? Status,
    DateOnly? From,
    DateOnly? To,
    int? Page,
    int? PageSize);
