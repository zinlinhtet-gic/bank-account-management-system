using bams.server.Models.Accounting;

namespace bams.server.DTO.Accounting;

public sealed record DailySummaryResponse(
    long Id,
    DateOnly SummaryDate,
    long GlAccountId,
    string GlAccountCode,
    string GlAccountName,
    decimal OpeningBalance,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance,
    DateTime GeneratedAt
);