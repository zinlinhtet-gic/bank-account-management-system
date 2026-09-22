using bams.server.Models.Accounting;

namespace bams.server.DTO.Accounting;
public sealed record GlAccountResponse(
    long Id,
    string Code,
    string Name,
    GlAccountClass AccountClass,
    long? ParentId,
    string Status
);