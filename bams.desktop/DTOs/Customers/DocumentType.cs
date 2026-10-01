namespace bams.desktop.DTOs.Customers;

/// <summary>
/// Mirrors the server's DocumentType enum. Member names must match exactly (JSON/form data travels as names).
/// </summary>
public enum DocumentType
{
    Nrc = 1,
    Passport = 2,
    Visa = 3,
    HouseholdRegistration = 4,
    ProofOfAddress = 5,
    TaxDocument = 6,
    SourceOfFunds = 7,
    Photo = 8
}
