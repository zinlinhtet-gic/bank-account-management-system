using bams.server.Models.Products;

namespace bams.server.DTO.Configuration;

public sealed record BankPolicyResponse(
    long Id,
    string Code,
    string Name,
    AccountTypeCategory Category,
    decimal MinimumOpeningBalance,
    decimal MinimumMaintainedBalance,
    decimal? DailyTransactionLimit,
    decimal? MonthlyTransactionLimit,
    decimal? WeeklyTransactionLimit,
    decimal? DailyWithdrawalLimit,
    decimal? MinimumDepositAmount,
    decimal? MinimumWithdrawalAmount,
    bool AllowWithdrawal,
    bool AllowTransfer,
    bool AllowPartialWithdrawal,
    bool AllowCitizen,
    bool AllowForeigner,
    int CitizenRequiredRefer,
    int ForeignRequiredRefer,
    string Status);
