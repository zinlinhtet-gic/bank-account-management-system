namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Fields the Customer Management create form actually collects. The server's real
/// CreateCustomerRequest has more optional fields (nationality, address, documents); they
/// are simply omitted from the multipart request when not supplied here (all optional server-side).
/// </summary>
public sealed record CreateCustomerRequest(
    CustomerType CustomerType,
    string FullName,
    DateOnly DateOfBirth,
    string? NrcNumber,
    string? PassportNumber,
    string? Phone,
    string? Email);
