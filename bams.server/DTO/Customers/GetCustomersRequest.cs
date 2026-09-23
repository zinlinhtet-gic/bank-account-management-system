using bams.server.Models.Customers;

namespace bams.server.DTO.Customers;

/// <summary>
/// Query parameters for listing customers: 1-based page number (page size is fixed by
/// <see cref="Constants.CustomerConstants.CustomersPageSize"/>) plus optional filters.
/// Each supplied filter narrows the result; omitted filters are not applied.
/// </summary>
public sealed record GetCustomersRequest(
    int PageNumber,
    string? CustomerNo,
    string? CustomerName,
    KycStatus? KycStatus,
    string? Status,
    RiskLevel? RiskLevel);
