namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's KycStatus enum. Member names must match exactly (JSON travels as names).
/// </summary>
public enum KycStatus
{
    Pending = 1,
    Verified = 2,
    Rejected = 3
}
