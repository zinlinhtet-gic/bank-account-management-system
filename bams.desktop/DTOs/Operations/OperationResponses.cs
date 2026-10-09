using bams.desktop.DTOs.Configuration;

namespace bams.desktop.DTOs.Operations;

/// <summary>Mirrors the server's <c>InterestOperationResponse</c>: one monthly interest accrual of one account.</summary>
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

/// <summary>Mirrors the server's <c>FeeOperationResponse</c>: one fee charged to one account.</summary>
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

/// <summary>Mirrors the server's <c>FixedDepositMaturityResponse</c>: one customer's fixed deposit.</summary>
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
