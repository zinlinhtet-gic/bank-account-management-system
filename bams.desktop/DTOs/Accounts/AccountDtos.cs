using System.Globalization;

namespace bams.desktop.DTOs.Accounts;

public sealed record AccountSummaryResponse(
    long Id,
    string AccountNo,
    string AccountTypeCode,
    string Status,
    decimal AvailableBalance);

public sealed record AccountResponse(
    long Id,
    string AccountNo,
    long AccountTypeId,
    string AccountTypeCode,
    string Status,
    decimal AvailableBalance,
    decimal LedgerBalance,
    DateTime OpenedAt,
    DateTime CreatedAt,
    long Version);

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

public sealed record InterestRateRuleResponse(
    long Id,
    long AccountTypeId,
    int? TermDays,
    int? TermMonths,
    decimal? BalanceMin,
    decimal? BalanceMax,
    decimal AnnualRate,
    decimal? EarlyWithdrawalRate,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo)
{
    public string DisplayName
    {
        get
        {
            var term = TermDays.HasValue
                ? $"{TermDays.Value} days"
                : TermMonths.HasValue
                    ? $"{TermMonths.Value} month{(TermMonths.Value == 1 ? string.Empty : "s")}"
                    : "Annual";
            return $"{term} — {AnnualRate.ToString("0.##", CultureInfo.CurrentCulture)}% p.a.";
        }
    }
}

public sealed record AccountPageResponse(
    IReadOnlyList<AccountSummaryResponse> Items,
    bool HasMore,
    string? NextCursor);

public sealed record AccountListCriteria(
    string? Search,
    long? AccountTypeId,
    string? Status,
    string? Cursor = null,
    int PageSize = 25);

public sealed record AccountDocumentUpload(
    string DocumentType,
    string? DocumentNumber,
    string FilePath);

public sealed record CreateAccountCommand(
    long AccountTypeId,
    decimal OpeningBalance,
    bool IsSharedAccount,
    string? HolderNrc1,
    string? HolderNrc2,
    decimal? OwnershipPercentage1,
    decimal? OwnershipPercentage2,
    string? SigningRule,
    IReadOnlyList<AccountDocumentUpload> Documents,
    long? PayoutAccountId,
    long? InterestRateRuleId,
    string? RenewalInstruction,
    bool? CalculateFromCurrent);
