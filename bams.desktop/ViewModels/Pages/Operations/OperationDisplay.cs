using bams.desktop.Constants;
using bams.desktop.DTOs.Configuration;
using bams.desktop.DTOs.Operations;
using bams.desktop.Models.Configuration;

namespace bams.desktop.ViewModels.Pages.Operations;

/// <summary>
/// Text formatting shared by the Operations pages, so every table shows customers, periods and money the same way.
/// </summary>
internal static class OperationDisplay
{
    /// <summary>Rows per page on every Operations list.</summary>
    public const int PageSize = 10;

    /// <summary>Active deposits maturing within this many days are highlighted as due soon.</summary>
    public const int DueSoonDays = 7;

    public static string FormatAmount(decimal amount) => amount.ToString(DisplayFormats.Amount);

    // Amount with the currency, e.g. "1,085.89 MMK", for the detail cards.
    public static string FormatMoney(decimal amount) => $"{FormatAmount(amount)} {DisplayFormats.CurrencyCode}";

    // Accrual month, e.g. "Sep 2026".
    public static string FormatMonth(DateOnly periodEnd) => periodEnd.ToString(MonthFormat);

    // e.g. "100 days" or "3 months".
    public static string FormatTerm(int? termDays, int? termMonths) =>
        termDays is { } days ? $"{days} days"
        : termMonths is { } months ? (months == 1 ? "1 month" : $"{months} months")
        : DisplayFormats.EmptyValue;

    // e.g. "TXN20260705… · 3,257.67 · 05/07/2026, 00:00", or "—" when there is no such transaction yet.
    public static string FormatTransactionLink(OperationTransactionLink? transaction) =>
        transaction is null
            ? DisplayFormats.EmptyValue
            : $"{transaction.TransactionNo} · {FormatAmount(transaction.Amount)} · {FormatOptionalDateTime(transaction.TransactionAt)}";

    // e.g. "Rule #3 · 1,000.00 MMK per month" or "Rule #4 · 0.50%".
    public static string FormatFeeRule(long feeRuleId, decimal? amount, decimal? percentage) =>
        amount is { } fixedAmount ? $"Rule #{feeRuleId} · {FormatMoney(fixedAmount)} per month"
        : percentage is { } rate ? $"Rule #{feeRuleId} · {FormatRate(rate)}"
        : $"Rule #{feeRuleId}";

    private const string MonthFormat = "MMM yyyy";

    // Rates are stored as percentages, e.g. 4.5 → "4.50%".
    public static string FormatRate(decimal annualRate) => $"{annualRate:0.00}%";

    public static string FormatDate(DateOnly date) => date.ToString(DisplayFormats.Date);

    public static string FormatOptionalDateTime(DateTime? value) =>
        value is null ? DisplayFormats.EmptyValue : value.Value.ToLocalTime().ToString(DisplayFormats.DateTime);

    public static string FormatPeriod(DateOnly start, DateOnly end) => $"{FormatDate(start)} – {FormatDate(end)}";

    public static string OrEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? DisplayFormats.EmptyValue : value;

    public static string FormatFeeType(FeeType feeType) =>
        FeeTypeOption.All.FirstOrDefault(option => option.Value == feeType)?.DisplayName ?? feeType.ToString();

    public static string FormatRenewalInstruction(RenewalInstruction instruction) => instruction switch
    {
        RenewalInstruction.NoRenewal => "No renewal",
        RenewalInstruction.PrincipalOnly => "Renew principal",
        RenewalInstruction.PrincipalAndInterest => "Renew principal + interest",
        _ => instruction.ToString()
    };

    // "5 days", "Today", or "Matured 3 days ago".
    public static string FormatDaysLeft(int daysToMaturity) => daysToMaturity switch
    {
        > 1 => $"{daysToMaturity} days",
        1 => "1 day",
        0 => "Today",
        -1 => "1 day ago",
        _ => $"{-daysToMaturity} days ago"
    };
}
