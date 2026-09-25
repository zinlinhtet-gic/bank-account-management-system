namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's RiskLevel enum. Member names must match exactly (JSON travels as names).
/// </summary>
public enum RiskLevel
{
    Low = 1,
    Medium = 2,
    High = 3
}
