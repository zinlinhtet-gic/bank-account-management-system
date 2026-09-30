using bams.desktop.DTOs.Common;

namespace bams.desktop.DTOs.Accounting;

public sealed record GlAccountDetailResponse(
    GlAccountResponse Account,
    PagedResponse<AccountingEntryResponse> Entries);
