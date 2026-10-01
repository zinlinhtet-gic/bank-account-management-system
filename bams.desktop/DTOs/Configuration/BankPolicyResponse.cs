namespace bams.desktop.DTOs.Configuration;

public sealed record BankPolicyResponse(
    long Id,
    string Code,
    string Name,
    AccountTypeCategory Category,
    decimal MinimumOpeningBalance,
    decimal MinimumMaintainedBalance,
    decimal? DailyTransactionLimit,
    decimal? MonthlyTransactionLimit,
    bool AllowWithdrawal,
    bool AllowTransfer,
    bool AllowPartialWithdrawal,
    bool AllowCitizen,
    bool AllowForeigner,
    int CitizenRequiredRefer,
    int ForeignRequiredRefer,
    string Status);
