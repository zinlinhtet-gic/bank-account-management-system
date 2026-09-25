using System.ComponentModel.DataAnnotations;
using bams.server.Models.Customers;

namespace bams.server.DTO.Customers;

/// <summary>Customer details submitted when registering a customer during account opening.</summary>
public sealed record CreateCustomerRequest(
    [param: Required, StringLength(150)] string FullName,
    DateOnly DateOfBirth,
    [param: Required, StringLength(32)] string NrcNumber,
    [param: StringLength(32)] string? Phone,
    [param: EmailAddress, StringLength(256)] string? Email,
    CustomerType CustomerType);
