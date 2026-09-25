namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's CustomerSummaryResponse (one row of GET /api/customers).
/// </summary>
public sealed record CustomerSummaryResponse(
    long Id,
    string CustomerNo,
    CustomerType CustomerType,
    string FullName,
    string? Phone,
    string? Email,
    KycStatus KycStatus,
    RiskLevel RiskLevel,
    string Status,
    DateTime CreatedAt);
