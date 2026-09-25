namespace bams.desktop.DTOs.Accounts;

public sealed record AccountListCriteria(
    string? Search,
    long? AccountTypeId,
    string? Status,
    string? Cursor = null,
    int PageSize = 10);
