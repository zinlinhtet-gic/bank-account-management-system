using bams.server.Models.Accounts.Enums;

namespace bams.server.DTO.Accounts;

public sealed record AccountHolderResponse(
    long Id,
    long AccountId,
    long CustomerId,
    string CustomerNrc,
    OwnershipType OwnershipType,
    decimal? OwnershipPercentage,
    bool IsPrimary,
    string? SigningRule,
    string Status,
    DateTime CreatedAt,
    long Version);
