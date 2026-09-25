using System.Globalization;

namespace bams.desktop.DTOs.Accounts;

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
