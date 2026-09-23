using bams.server.Models.Customers;

namespace bams.server.DTO.Customers;

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
    List<CustomerDocument> Documents);
