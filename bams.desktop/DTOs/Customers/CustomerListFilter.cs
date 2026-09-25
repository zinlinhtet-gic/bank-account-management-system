namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Filters from the Customer List filter bar. The table component adds the page number to build
/// the actual <see cref="GetCustomersRequest"/> sent to the server.
/// </summary>
public sealed record CustomerListFilter(
    string? CustomerNo,
    string? CustomerName,
    KycStatus? KycStatus,
    string? Status,
    RiskLevel? RiskLevel,
    DateOnly? StartDate,
    DateOnly? EndDate);
