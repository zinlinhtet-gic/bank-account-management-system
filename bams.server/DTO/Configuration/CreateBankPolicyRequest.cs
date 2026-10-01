using bams.server.Models.Products;

namespace bams.server.DTO.Configuration;

public sealed record CreateBankPolicyRequest(
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
