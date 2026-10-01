namespace bams.desktop.DTOs.Accounts;
using bams.desktop.DTOs.Customers;
public sealed record CustomerLookupResponse(
    long Id,
    string CustomerNo,
    string FullName,
    DateOnly DateOfBirth,
    string? NrcNumber,
    string? Phone,
    string? Email,
    string Status,
    CustomerType CustomerType,
    KycStatus KycStatus);
