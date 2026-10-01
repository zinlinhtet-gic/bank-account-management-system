namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's CustomerResponse (full detail, returned by create/update/get-by-id).
/// </summary>
public sealed record CustomerResponse(
    long Id,
    string CustomerNo,
    CustomerType CustomerType,
    string FullName,
    DateOnly DateOfBirth,
    string? Nationality,
    string? NrcNumber,
    string? PassportNumber,
    string? Phone,
    string? Occupation,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Country,
    string? Email,
    RiskLevel RiskLevel,
    KycStatus KycStatus,
    string Status,
    DateTime CreatedAt,
    List<CustomerDocumentResponse> Documents);
