using bams.desktop.DTOs.Accounts;

namespace bams.desktop.DTOs.Customers;

public sealed record CreateCustomerRequest(
    string FullName,
    DateOnly DateOfBirth,
    string NrcNumber,
    string? Phone,
    string? Email,
    CustomerType CustomerType);
