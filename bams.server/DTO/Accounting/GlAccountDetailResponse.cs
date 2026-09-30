using bams.server.DTO.Common;

namespace bams.server.DTO.Accounting;

public sealed record GlAccountDetailResponse(
    GlAccountResponse Account,
    PagedResponse<AccountingEntryResponse> Entries);
