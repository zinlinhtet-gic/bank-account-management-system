namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's CustomerType enum. Member names must match exactly (JSON travels as names).
/// </summary>
public enum CustomerType
{
    Citizen = 1,
    Foreigner = 2
}
