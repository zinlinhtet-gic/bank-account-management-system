namespace bams.desktop.DTOs.Accounts;

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
    bool IsFixedDeposit,
    bool AllowForeigner,
    bool AllowCitizen,
    int CitizenRequiredRefer,
    int ForeignRequiredRefer);
