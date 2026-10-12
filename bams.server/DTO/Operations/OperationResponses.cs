using bams.server.Models.Accounts.Enums;
using bams.server.Models.InterestFees;
using bams.server.Models.Products;

namespace bams.server.DTO.Operations;

/// <summary>
/// One monthly interest accrual of one customer account. The customer is the account's primary holder.
/// </summary>
public sealed record InterestOperationResponse(
    long Id,
    long AccountId,
    string AccountNo,
    string AccountTypeCode,
    string? CustomerNo,
    string? CustomerName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal CalculationBalance,
    decimal AnnualRate,
    decimal Amount,
    string Status,
    DateTime CalculatedAt,
    DateTime? PostedAt);

/// <summary>
/// One fee charged to one customer account (maintenance, early withdrawal, dormant penalty, ...).
/// The customer is the account's primary holder.
/// </summary>
public sealed record FeeOperationResponse(
    long Id,
    long AccountId,
    string AccountNo,
    string AccountTypeCode,
    string? CustomerNo,
    string? CustomerName,
    FeeType FeeType,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Amount,
    decimal TaxAmount,
    FeeAccrualStatus Status,
    DateTime CalculatedAt,
    DateTime? PostedAt);

/// <summary>
/// One customer's fixed deposit with its maturity details. The customer is the account's primary holder.
/// </summary>
/// <param name="DaysToMaturity">Days from today (business date) to maturity; zero or negative once matured.</param>
/// <param name="InterestAccrued">Interest accrued so far within this deposit's term.</param>
/// <param name="ExpectedMaturityInterest">Interest for the full term on the current principal (actual/365).</param>
public sealed record FixedDepositMaturityResponse(
    long Id,
    long AccountId,
    string AccountNo,
    string AccountTypeCode,
    string? CustomerNo,
    string? CustomerName,
    decimal PrincipalAmount,
    decimal AnnualRate,
    DateOnly StartDate,
    DateOnly MaturityDate,
    int DaysToMaturity,
    decimal InterestAccrued,
    decimal ExpectedMaturityInterest,
    RenewalInstruction RenewalInstruction,
    string? PayoutAccountNo,
    FixedDepositStatus Status);
