using bams.server.Models.Accounting;
namespace bams.server.DTO.Accounting;

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