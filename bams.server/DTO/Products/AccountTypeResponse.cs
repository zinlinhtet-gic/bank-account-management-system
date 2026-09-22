namespace bams.server.DTO.Products;

/// <summary>
/// Represents an account product currently available for account opening.
/// </summary>
public sealed record AccountTypeResponse(
    long Id,
    string Code,
    string Name,
    string? Category,
    decimal MinimumOpeningBalance,
    decimal MinimumMaintainedBalance,
    decimal? DailyTransactionLimit,
    decimal? MonthlyTransactionLimit,
    bool AllowWithdrawal,
    bool AllowTransfer,
    bool AllowPartialWithdrawal,
    long? RequiredProductId,
    bool IsFixedDeposit);
