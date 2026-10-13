namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's ReviewCustomerKycRequest (POST /api/customers/{id}/kyc-review). The status
/// must be <see cref="KycStatus.Verified"/> or <see cref="KycStatus.Rejected"/> — a review always
/// decides one or the other, never resets a customer back to Pending.
/// </summary>
public sealed record ReviewCustomerKycRequest(KycStatus KycStatus);
