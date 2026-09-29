namespace bams.desktop.DTOs.Accounts;

public sealed record InterestAccrualResponse(
    long Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal CalculationBalance,
    decimal AnnualRate,
    decimal CalculatedAmount,
    string Status,
    DateTime CalculatedAt,
    DateTime? PostedAt);
