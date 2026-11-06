namespace bams.server.DTO.Accounts;

/// <summary>Basic identity of an account holder.</summary>
public sealed record AccountCustomerResponse(
    long CustomerId, string CustomerNo, string FullName,
    string? NrcNumber, string? PassportNumber, bool IsPrimary);
