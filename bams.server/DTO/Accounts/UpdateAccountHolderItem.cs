namespace bams.server.DTO.Accounts;

public sealed record UpdateAccountHolderItem(
    long AccountHolderId,
    decimal OwnershipPercentage,
    bool IsPrimary);
