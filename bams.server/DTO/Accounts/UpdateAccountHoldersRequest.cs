namespace bams.server.DTO.Accounts;

public sealed record UpdateAccountHoldersRequest(
    IReadOnlyList<UpdateAccountHolderItem> Holders,
    string? SigningRule,
    long AccountVersion);
