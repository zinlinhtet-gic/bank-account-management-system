using bams.server.Models.Customers;

namespace bams.server.DTO.Customers;

/// <summary>
/// Records a KYC review decision for a customer. The status must be
/// <see cref="KycStatus.Verified"/> or <see cref="KycStatus.Rejected"/> — a review always
/// decides one or the other, never resets a customer back to Pending.
/// </summary>
public sealed record ReviewCustomerKycRequest(
    KycStatus KycStatus,
    long ReviewedByUserId);
