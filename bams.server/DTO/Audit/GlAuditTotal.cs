namespace bams.server.DTO.Audit;

public sealed record GlAuditTotal
(
    long GlAccountId,
    decimal TotalDebit,
    decimal TotalCredit
);