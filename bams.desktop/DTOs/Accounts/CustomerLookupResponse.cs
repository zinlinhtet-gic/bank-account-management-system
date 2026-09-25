namespace bams.desktop.DTOs.Accounts;

public sealed record CustomerLookupResponse(
    long Id,
    string CustomerNo,
    string FullName,
    DateOnly DateOfBirth,
    string? NrcNumber,
    string? Phone,
    string? Email,
    string Status,
    CustomerType CustomerType);
