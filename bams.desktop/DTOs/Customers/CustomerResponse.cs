namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the fields of the server's CustomerResponse that this client actually uses
/// (full detail, returned by create/update). Documents and address fields are not
/// needed by any screen yet, so they are left out per the DTO "only what the screen needs" rule.
/// </summary>
public sealed record CustomerResponse(
    long Id,
    string CustomerNo,
    CustomerType CustomerType,
    string FullName,
    DateOnly DateOfBirth,
    string? NrcNumber,
    string? PassportNumber,
    string? Phone,
    string? Email,
    RiskLevel RiskLevel,
    KycStatus KycStatus,
    string Status,
    DateTime CreatedAt);
