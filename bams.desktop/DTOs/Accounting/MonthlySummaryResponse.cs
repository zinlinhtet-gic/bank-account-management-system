namespace bams.desktop.DTOs.Accounting;

/// <summary>One GL account's totals for one accounting month, as returned by GET api/accounting/monthly-summaries.</summary>
public sealed record MonthlySummaryResponse(
    long Id,
    int Year,
    int Month,
    long GlAccountId,
    string GlAccountCode,
    string GlAccountName,
    decimal OpeningBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance,
    DateTime GeneratedAt
);
