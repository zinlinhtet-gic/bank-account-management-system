namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's GetCustomersRequest query contract (GET /api/customers). Every filter
/// is optional; only supplied (non-null) filters are sent.
/// </summary>
public sealed record GetCustomersRequest(
    int PageNumber,
    string? CustomerNo = null,
    string? CustomerName = null,
    KycStatus? KycStatus = null,
    string? Status = null,
    RiskLevel? RiskLevel = null,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null);
