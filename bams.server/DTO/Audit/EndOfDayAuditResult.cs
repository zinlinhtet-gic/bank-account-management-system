namespace bams.server.DTO.Audit;

/// <summary>
/// Represents the result of an end-of-day audit.
/// </summary>
public sealed record EndOfDayAuditResult
(
    DateOnly AuditDate,
    int RetrievedTransactionsCount,
    int IncludedTransactionCount,
    int ExcludedTransactionCount,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    bool IsReconciled
);