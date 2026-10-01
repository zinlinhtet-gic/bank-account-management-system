using bams.desktop.DTOs.Customers;

namespace bams.desktop.Utils;

/// <summary>
/// Friendly labels for <see cref="DocumentType"/>, shared by the create form and the customer details card.
/// </summary>
public static class DocumentTypeDisplay
{
    public static string ToDisplayName(DocumentType documentType)
    {
        return documentType switch
        {
            DocumentType.Nrc => "NRC",
            DocumentType.Passport => "Passport",
            DocumentType.Visa => "Visa",
            DocumentType.HouseholdRegistration => "Household Registration",
            DocumentType.ProofOfAddress => "Proof of Address",
            DocumentType.TaxDocument => "Tax Document",
            DocumentType.SourceOfFunds => "Source of Funds",
            DocumentType.Photo => "Photo",
            _ => documentType.ToString()
        };
    }
}
