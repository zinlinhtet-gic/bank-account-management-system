using bams.desktop.DTOs.Configuration;

namespace bams.desktop.DTOs.Operations;

/// <summary>Filters for the Interest operations list. Null values are not sent.</summary>
public sealed record InterestOperationFilter(
    string? Search,
    InterestAccrualStatus? Status,
    DateOnly? From,
    DateOnly? To);

/// <summary>Filters for the Fees operations list. Null values are not sent.</summary>
public sealed record FeeOperationFilter(
    string? Search,
    FeeType? FeeType,
    FeeAccrualStatus? Status,
    DateOnly? From,
    DateOnly? To);

/// <summary>Filters for the Fixed Deposit Maturity list; From/To bound the maturity date.</summary>
public sealed record FixedDepositMaturityFilter(
    string? Search,
    FixedDepositStatus? Status,
    DateOnly? From,
    DateOnly? To);
