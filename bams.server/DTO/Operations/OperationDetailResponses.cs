using bams.server.Models.Accounts.Enums;
using bams.server.Models.InterestFees;
using bams.server.Models.Products;

namespace bams.server.DTO.Operations;

/// <summary>
/// The account an Operations record belongs to, with its primary holder's contact details.
/// Customer fields are null when the account has no primary holder.
/// </summary>
public sealed record OperationAccountDetail(
    long AccountId,
    string AccountNo,
    string AccountTypeCode,
    string AccountTypeName,
    AccountStatus AccountStatus,
    decimal LedgerBalance,
    string? CustomerNo,
    string? CustomerName,
    string? CustomerNrc,
    string? CustomerPhone);

/// <summary>
/// A transaction linked to an Operations record: the accrual entry or the posting that credited / deducted it.
/// </summary>
public sealed record OperationTransactionLink(
    long TransactionId,
    string TransactionNo,
    decimal Amount,
    DateTime TransactionAt);

/// <summary>
/// One interest accrual with its account, the rule it used and the transactions that recorded and credited it.
/// </summary>
/// <param name="AccruedTransaction">The month-end accrual entry; null until the accrual transaction is written.</param>
/// <param name="PostedTransaction">The quarterly credit that paid this accrual (with others); null while Accrued.</param>
public sealed record InterestOperationDetailResponse(
    long Id,
    OperationAccountDetail Account,
    long InterestRateRuleId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal CalculationBalance,
    decimal AnnualRate,
    decimal Amount,
    string Status,
    DateTime CalculatedAt,
    DateTime? PostedAt,
    OperationTransactionLink? AccruedTransaction,
    OperationTransactionLink? PostedTransaction);

/// <summary>
/// One fee accrual with its account, the fee rule it used and the transactions that recorded and deducted it.
/// </summary>
/// <param name="RuleAmount">Fixed amount of the fee rule; null for a percentage rule.</param>
/// <param name="RulePercentage">Percentage of the fee rule; null for a fixed-amount rule.</param>
/// <param name="PostedTransaction">The posting that deducted this fee (with others); null while Accrued.</param>
public sealed record FeeOperationDetailResponse(
    long Id,
    OperationAccountDetail Account,
    FeeType FeeType,
    long FeeRuleId,
    decimal? RuleAmount,
    decimal? RulePercentage,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    decimal TaxAmount,
    FeeAccrualStatus Status,
    DateTime CalculatedAt,
    DateTime? PostedAt,
    OperationTransactionLink? AccruedTransaction,
    OperationTransactionLink? PostedTransaction);

/// <summary>One month of a fixed deposit's interest schedule.</summary>
public sealed record FixedDepositInterestLine(
    long AccrualId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    string Status,
    DateTime? PostedAt);

/// <summary>
/// One fixed deposit with its term, principal history, payout account and month-by-month interest schedule.
/// </summary>
/// <param name="DaysToMaturity">Days from today (business date) to maturity; zero or negative once matured.</param>
/// <param name="InterestPosted">Interest already credited within this deposit's term.</param>
public sealed record FixedDepositMaturityDetailResponse(
    long Id,
    OperationAccountDetail Account,
    decimal OriginalPrincipal,
    decimal CurrentPrincipal,
    decimal AnnualRate,
    int? TermDays,
    int? TermMonths,
    DateOnly StartDate,
    DateOnly MaturityDate,
    int DaysToMaturity,
    decimal InterestAccrued,
    decimal InterestPosted,
    decimal ExpectedMaturityInterest,
    RenewalInstruction RenewalInstruction,
    string? PayoutAccountNo,
    FixedDepositStatus Status,
    DateTime CreatedAt,
    IReadOnlyList<FixedDepositInterestLine> InterestSchedule);
